Option Explicit On
Option Strict On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq
Imports Bytery.Linq

Namespace Benchmark.Tests

    Public MustInherit Class Test

        Public MustOverride ReadOnly Property Name As String
        Public MustOverride ReadOnly Property Description As String
        Public MustOverride ReadOnly Property Iterations As Integer

        ' Casos que devem passar normalmente
        Public MustOverride Function BuildObjects() As List(Of Object)

        ' Casos que DEVEM lançar exceção
        Public Overridable Function BuildExpectedFailures() As List(Of ExpectedFailureCase)
            Return New List(Of ExpectedFailureCase)()
        End Function

        Public Overridable ReadOnly Property Threads As Integer
            Get
                Return 1
            End Get
        End Property

        Public Overridable ReadOnly Property IncludeNewtonsoftActors As Boolean
            Get
                Return True
            End Get
        End Property

        Private Function CreateActors() As List(Of TestActor)

            Dim actors As New List(Of TestActor)

            If IncludeNewtonsoftActors Then
                actors.Add(New TestActor.ActorNewtonsoft())
                actors.Add(New TestActor.ActorNewtonsoftGZip())
            End If

            actors.Add(New TestActor.ActorByteryRaw())
            actors.Add(New TestActor.ActorByteryVV())
            actors.Add(New TestActor.ActorBytery())

            Return actors

        End Function

        Public Function Run() As Summary

            Dim objs As List(Of Object) = Me.BuildObjects()
            If objs Is Nothing Then objs = New List(Of Object)()

            Dim failures As List(Of ExpectedFailureCase) = Me.BuildExpectedFailures()
            If failures Is Nothing Then failures = New List(Of ExpectedFailureCase)()

            ' 1) valida casos que devem passar
            ValidateByteryEquivalence(objs)

            ' 2) valida casos que devem falhar
            ValidateExpectedFailures(failures)

            Dim threadCount As Integer = Math.Max(1, Me.Threads)

            Dim result As New Summary(Me)
            result.ThreadsUsed = threadCount

            Dim perThread(threadCount - 1) As List(Of TestActor.Snapshot)

            Parallel.For(
                0, threadCount,
                Sub(tid As Integer)

                    Dim actors As List(Of TestActor) = CreateActors()

                    Dim local As New List(Of TestActor.Snapshot)(actors.Count)

                    For Each a In actors
                        a.Warmup(objs)

                        For Each o As Object In objs
                            a.Run(Me.Iterations, o)
                        Next

                        local.Add(a.ToSnapshot())
                    Next

                    perThread(tid) = local

                End Sub
            )

            For i As Integer = 0 To perThread.Length - 1
                Dim local = perThread(i)
                If local Is Nothing Then Continue For

                For Each snap As TestActor.Snapshot In local
                    If Not result.Snapshots.ContainsKey(snap.ActorName) Then
                        result.Snapshots.Add(snap.ActorName, snap)
                    Else
                        result.Snapshots(snap.ActorName).Sum(snap)
                    End If
                Next
            Next

            Return result

        End Function

#Region "Expected failure validation"

        Public NotInheritable Class ExpectedFailureCase
            Public Property Name As String
            Public Property Factory As Func(Of Object)
            Public Property ExpectedExceptionType As Type
            Public Property MessageContains As String
        End Class

        Private Shared Sub ValidateExpectedFailures(cases As List(Of ExpectedFailureCase))

            If cases Is Nothing OrElse cases.Count = 0 Then Return

            For i As Integer = 0 To cases.Count - 1

                Dim c As ExpectedFailureCase = cases(i)

                If c Is Nothing Then
                    Throw New Exception("ExpectedFailureCase cannot be Nothing.")
                End If

                If c.Factory Is Nothing Then
                    Throw New Exception("ExpectedFailureCase.Factory cannot be Nothing.")
                End If

                If c.ExpectedExceptionType Is Nothing Then
                    Throw New Exception("ExpectedFailureCase.ExpectedExceptionType cannot be Nothing.")
                End If

                Dim instance As Object = c.Factory.Invoke()

                Try
                    Dim bytes() As Byte = Bytery.Encode(instance)
                    Throw New Exception($"Expected failure case '{c.Name}' did not throw. Encode succeeded and produced {If(bytes Is Nothing, 0, bytes.Length)} byte(s).")

                Catch ex As Exception

                    If Not c.ExpectedExceptionType.IsAssignableFrom(ex.GetType()) Then
                        Throw New Exception(
                            $"Expected failure case '{c.Name}' threw wrong exception type. Expected {c.ExpectedExceptionType.FullName}, got {ex.GetType().FullName}. Message: {ex.Message}")
                    End If

                    If Not String.IsNullOrEmpty(c.MessageContains) Then
                        If ex.Message Is Nothing OrElse
                           ex.Message.IndexOf(c.MessageContains, StringComparison.OrdinalIgnoreCase) < 0 Then

                            Throw New Exception(
                                $"Expected failure case '{c.Name}' threw correct exception type but wrong message. Expected message containing '{c.MessageContains}', got '{ex.Message}'.")
                        End If
                    End If

                End Try

            Next

        End Sub

#End Region

#Region "Equivalence validation"

        Private Shared Sub ValidateByteryEquivalence(objs As List(Of Object))

            If objs Is Nothing Then Return

            For i As Integer = 0 To objs.Count - 1

                Dim source As Object = objs(i)

                Dim expected As JToken = ToNewtonsoftToken(source)

                Dim bytes() As Byte = Bytery.Encode(source)
                Dim decoded As BToken = Bytery.Decode(bytes)

                AssertEquivalent(expected, decoded, "$")

                Dim bytesFromBToken() As Byte = Bytery.Encode(decoded)
                Dim decodedAgain As BToken = Bytery.Decode(bytesFromBToken)

                AssertBTokenEquivalent(decoded, decodedAgain, "$")

            Next

        End Sub

        Private Shared Sub AssertBTokenEquivalent(expected As BToken, actual As BToken, path As String)

            If expected Is Nothing Then
                If actual Is Nothing Then Return
                Throw New Exception($"Mismatch at {path}: expected Nothing, got {DescribeToken(actual)}.")
            End If

            If expected.IsNull Then
                If actual Is Nothing OrElse Not actual.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected null, got {DescribeToken(actual)}.")
                End If

                If expected.FieldCode <> actual.FieldCode Then
                    Throw New Exception($"Mismatch at {path}: null FieldCode differs. Expected {expected.FieldCode}, got {actual.FieldCode}.")
                End If

                Return
            End If

            If TypeOf expected Is BObject Then

                Dim expObj As BObject = DirectCast(expected, BObject)
                Dim actObj As BObject = TryCast(actual, BObject)

                If actObj Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected BObject, got {DescribeToken(actual)}.")
                End If

                If expObj.ObjectKind <> actObj.ObjectKind Then
                    Throw New Exception($"Mismatch at {path}: ObjectKind differs. Expected {expObj.ObjectKind}, got {actObj.ObjectKind}.")
                End If

                If expObj.MapValueFieldCode <> actObj.MapValueFieldCode Then
                    Throw New Exception($"Mismatch at {path}: MapValueFieldCode differs. Expected {expObj.MapValueFieldCode}, got {actObj.MapValueFieldCode}.")
                End If

                If expObj.Count <> actObj.Count Then
                    Throw New Exception($"Mismatch at {path}: object property count differs. Expected {expObj.Count}, got {actObj.Count}.")
                End If

                For Each kv As KeyValuePair(Of String, BToken) In expObj
                    Dim child As BToken = Nothing

                    If Not actObj.TryGetDirectValue(kv.Key, child) Then
                        Throw New Exception($"Mismatch at {path}: property not found: ""{kv.Key}"".")
                    End If

                    AssertBTokenEquivalent(kv.Value, child, path & "." & kv.Key)
                Next

                Return

            End If

            If TypeOf expected Is BArray Then

                Dim expArr As BArray = DirectCast(expected, BArray)
                Dim actArr As BArray = TryCast(actual, BArray)

                If actArr Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected BArray, got {DescribeToken(actual)}.")
                End If

                If expArr.FieldCode <> actArr.FieldCode Then
                    Throw New Exception($"Mismatch at {path}: array FieldCode differs. Expected {expArr.FieldCode}, got {actArr.FieldCode}.")
                End If

                If expArr.Count <> actArr.Count Then
                    Throw New Exception($"Mismatch at {path}: array length differs. Expected {expArr.Count}, got {actArr.Count}.")
                End If

                For j As Integer = 0 To expArr.Count - 1
                    AssertBTokenEquivalent(expArr(j), actArr(j), path & "[" & j.ToString(CultureInfo.InvariantCulture) & "]")
                Next

                Return

            End If

            If TypeOf expected Is BNumber Then

                Dim expNum As BNumber = DirectCast(expected, BNumber)
                Dim actNum As BNumber = TryCast(actual, BNumber)

                If actNum Is Nothing OrElse actNum.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected BNumber, got {DescribeToken(actual)}.")
                End If

                Dim expValue As Double = expNum.GetValue(Of Double)()
                Dim actValue As Double = actNum.GetValue(Of Double)()

                If expValue.ToString("R", CultureInfo.InvariantCulture) <> actValue.ToString("R", CultureInfo.InvariantCulture) Then
                    Throw New Exception($"Mismatch at {path}: expected number {expValue.ToString("R", CultureInfo.InvariantCulture)}, got {actValue.ToString("R", CultureInfo.InvariantCulture)}.")
                End If

                Return

            End If

            If TypeOf expected Is BBoolean Then

                Dim expBool As BBoolean = DirectCast(expected, BBoolean)
                Dim actBool As BBoolean = TryCast(actual, BBoolean)

                If actBool Is Nothing OrElse Not expBool.Value.HasValue OrElse Not actBool.Value.HasValue Then
                    Throw New Exception($"Mismatch at {path}: expected BBoolean, got {DescribeToken(actual)}.")
                End If

                If expBool.Value.Value <> actBool.Value.Value Then
                    Throw New Exception($"Mismatch at {path}: expected Boolean {expBool.Value.Value}, got {actBool.Value.Value}.")
                End If

                Return

            End If

            If TypeOf expected Is BString Then

                Dim expStr As BString = DirectCast(expected, BString)
                Dim actStr As BString = TryCast(actual, BString)

                If actStr Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected BString, got {DescribeToken(actual)}.")
                End If

                If expStr.Value <> actStr.Value Then
                    Throw New Exception($"Mismatch at {path}: expected String ""{expStr.Value}"", got ""{actStr.Value}"".")
                End If

                Return

            End If

            If TypeOf expected Is BDate Then

                Dim expDate As BDate = DirectCast(expected, BDate)
                Dim actDate As BDate = TryCast(actual, BDate)

                If actDate Is Nothing OrElse actDate.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected BDate, got {DescribeToken(actual)}.")
                End If

                Dim d1 As DateTime = expDate.Value
                Dim d2 As DateTime = actDate.Value

                If d1.Kind = DateTimeKind.Unspecified Then d1 = DateTime.SpecifyKind(d1, DateTimeKind.Utc)
                If d2.Kind = DateTimeKind.Unspecified Then d2 = DateTime.SpecifyKind(d2, DateTimeKind.Utc)

                d1 = d1.ToUniversalTime()
                d2 = d2.ToUniversalTime()

                If d1 <> d2 Then
                    Throw New Exception($"Mismatch at {path}: expected Date {d1.ToString("o", CultureInfo.InvariantCulture)}, got {d2.ToString("o", CultureInfo.InvariantCulture)}.")
                End If

                Return

            End If

            If TypeOf expected Is BBytes Then

                Dim expBytes As BBytes = DirectCast(expected, BBytes)
                Dim actBytes As BBytes = TryCast(actual, BBytes)

                If actBytes Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected BBytes, got {DescribeToken(actual)}.")
                End If

                Dim b1() As Byte = expBytes.Value
                Dim b2() As Byte = actBytes.Value

                If b1 Is Nothing AndAlso b2 Is Nothing Then Return
                If b1 Is Nothing Xor b2 Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: one byte array is null and the other is not.")
                End If

                If b1.Length <> b2.Length Then
                    Throw New Exception($"Mismatch at {path}: byte array length differs. Expected {b1.Length}, got {b2.Length}.")
                End If

                For j As Integer = 0 To b1.Length - 1
                    If b1(j) <> b2(j) Then
                        Throw New Exception($"Mismatch at {path}: byte array differs at index {j}. Expected {b1(j)}, got {b2(j)}.")
                    End If
                Next

                Return

            End If

            Throw New NotSupportedException($"Unsupported BToken type in equivalence check at {path}: {expected.GetType().FullName}.")

        End Sub

        Private Shared Function DescribeToken(token As BToken) As String
            If token Is Nothing Then Return "Nothing"
            Return token.GetType().Name
        End Function

        Private Shared Function ToNewtonsoftToken(value As Object) As JToken
            If value Is Nothing Then
                Return JValue.CreateNull()
            End If

            Return JToken.FromObject(value)
        End Function

        Private Shared Sub AssertEquivalent(expected As JToken, actual As BToken, path As String)

            If expected Is Nothing Then
                If actual Is Nothing OrElse actual.IsNull Then Return
                Throw New Exception($"Mismatch at {path}: expected null, got {actual.GetType().Name}.")
            End If

            Select Case expected.Type

                Case JTokenType.Null, JTokenType.Undefined

                    If actual Is Nothing OrElse actual.IsNull Then Return
                    Throw New Exception($"Mismatch at {path}: expected null, got {actual.GetType().Name}.")

                Case JTokenType.Object

                    Dim expObj As JObject = DirectCast(expected, JObject)
                    Dim actObj As BObject = TryCast(actual, BObject)

                    If actObj Is Nothing Then
                        Throw New Exception($"Mismatch at {path}: expected Object, got {DescribeToken(actual)}.")
                    End If

                    If expObj.Count <> actObj.Count Then
                        Throw New Exception($"Mismatch at {path}: object property count differs. Expected {expObj.Count}, got {actObj.Count}.")
                    End If

                    For Each p As JProperty In expObj.Properties()
                        Dim child As BToken = Nothing

                        If Not actObj.TryGetDirectValue(p.Name, child) Then
                            Throw New Exception($"Mismatch at {path}: property not found in Bytery object: ""{p.Name}"".")
                        End If

                        AssertEquivalent(p.Value, child, path & "." & p.Name)
                    Next

                Case JTokenType.Array

                    Dim expArr As JArray = DirectCast(expected, JArray)
                    Dim actArr As BArray = TryCast(actual, BArray)

                    If actArr Is Nothing Then
                        Throw New Exception($"Mismatch at {path}: expected Array, got {DescribeToken(actual)}.")
                    End If

                    If expArr.Count <> actArr.Count Then
                        Throw New Exception($"Mismatch at {path}: array length differs. Expected {expArr.Count}, got {actArr.Count}.")
                    End If

                    For i As Integer = 0 To expArr.Count - 1
                        AssertEquivalent(expArr(i), actArr(i), path & "[" & i.ToString(CultureInfo.InvariantCulture) & "]")
                    Next

                Case JTokenType.Integer, JTokenType.Float

                    Dim actNum As BNumber = TryCast(actual, BNumber)
                    If actNum Is Nothing OrElse actNum.IsNull Then
                        Throw New Exception($"Mismatch at {path}: expected Number, got {DescribeToken(actual)}.")
                    End If

                    Dim expValue As Double = Convert.ToDouble(DirectCast(expected, JValue).Value, CultureInfo.InvariantCulture)
                    Dim actValue As Double = actNum.GetValue(Of Double)

                    If expValue.ToString("R", CultureInfo.InvariantCulture) <> actValue.ToString("R", CultureInfo.InvariantCulture) Then
                        Throw New Exception($"Mismatch at {path}: expected number {expValue.ToString("R", CultureInfo.InvariantCulture)}, got {actValue.ToString("R", CultureInfo.InvariantCulture)}.")
                    End If

                Case JTokenType.Boolean

                    Dim actBool As BBoolean = TryCast(actual, BBoolean)
                    If actBool Is Nothing OrElse Not actBool.Value.HasValue Then
                        Throw New Exception($"Mismatch at {path}: expected Boolean, got {DescribeToken(actual)}.")
                    End If

                    Dim expValue As Boolean = Convert.ToBoolean(DirectCast(expected, JValue).Value, CultureInfo.InvariantCulture)
                    If expValue <> actBool.Value.Value Then
                        Throw New Exception($"Mismatch at {path}: expected Boolean {expValue}, got {actBool.Value.Value}.")
                    End If

                Case JTokenType.String

                    Dim actStr As BString = TryCast(actual, BString)
                    If actStr Is Nothing Then
                        Throw New Exception($"Mismatch at {path}: expected String, got {DescribeToken(actual)}.")
                    End If

                    Dim expValue As String = DirectCast(expected, JValue).Value(Of String)()
                    If expValue <> actStr.Value Then
                        Throw New Exception($"Mismatch at {path}: expected String ""{expValue}"", got ""{actStr.Value}"".")
                    End If

                Case JTokenType.Date

                    Dim actDate As BDate = TryCast(actual, BDate)
                    If actDate Is Nothing OrElse actDate.IsNull Then
                        Throw New Exception($"Mismatch at {path}: expected Date, got {DescribeToken(actual)}.")
                    End If

                    Dim raw As Object = DirectCast(expected, JValue).Value
                    Dim expDate As DateTime

                    If TypeOf raw Is DateTimeOffset Then
                        expDate = DirectCast(raw, DateTimeOffset).UtcDateTime
                    Else
                        expDate = CType(raw, DateTime)
                        If expDate.Kind = DateTimeKind.Unspecified Then
                            expDate = DateTime.SpecifyKind(expDate, DateTimeKind.Utc)
                        End If
                        expDate = expDate.ToUniversalTime()
                    End If

                    Dim gotDate As DateTime = actDate.Value
                    If gotDate.Kind = DateTimeKind.Unspecified Then
                        gotDate = DateTime.SpecifyKind(gotDate, DateTimeKind.Utc)
                    End If
                    gotDate = gotDate.ToUniversalTime()

                    If expDate <> gotDate Then
                        Throw New Exception($"Mismatch at {path}: expected Date {expDate.ToString("o", CultureInfo.InvariantCulture)}, got {gotDate.ToString("o", CultureInfo.InvariantCulture)}.")
                    End If

                Case JTokenType.Bytes

                    Dim actBytes As BBytes = TryCast(actual, BBytes)
                    If actBytes Is Nothing Then
                        Throw New Exception($"Mismatch at {path}: expected Bytes, got {DescribeToken(actual)}.")
                    End If

                    Dim expBytes() As Byte = DirectCast(DirectCast(expected, JValue).Value, Byte())
                    Dim gotBytes() As Byte = actBytes.Value

                    If expBytes Is Nothing AndAlso gotBytes Is Nothing Then Return

                    If expBytes Is Nothing Xor gotBytes Is Nothing Then
                        Throw New Exception($"Mismatch at {path}: one byte array is null and the other is not.")
                    End If

                    If expBytes.Length <> gotBytes.Length Then
                        Throw New Exception($"Mismatch at {path}: byte array length differs. Expected {expBytes.Length}, got {gotBytes.Length}.")
                    End If

                    For i As Integer = 0 To expBytes.Length - 1
                        If expBytes(i) <> gotBytes(i) Then
                            Throw New Exception($"Mismatch at {path}: byte array differs at index {i}. Expected {expBytes(i)}, got {gotBytes(i)}.")
                        End If
                    Next

                Case Else
                    Throw New NotSupportedException($"Unsupported Newtonsoft token type in equivalence check at {path}: {expected.Type}.")

            End Select

        End Sub

#End Region

    End Class

End Namespace