Imports System.Globalization
Imports System.IO
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports Bytery.Linq

Namespace Benchmark.Tests

    Public NotInheritable Class ParserTests

        Private Sub New()
        End Sub

        Private NotInheritable Class ParserCase
            Public Property Name As String
            Public Property Value As Object
        End Class

#Region "Public Runner"

        Public Shared Sub RunParserSuite(Optional randomCases As Integer = 200,
                                         Optional seed As Integer = 12345)

            Console.WriteLine()
            Console.WriteLine("======================================")
            Console.WriteLine("PARSER CONSISTENCY SUITE (Newtonsoft vs BToken.Parse)")
            Console.WriteLine("======================================")

            Dim cases As New List(Of ParserCase)
            cases.AddRange(BuildFixedCases())

            Dim rng As New Random(seed)
            For i As Integer = 1 To randomCases
                cases.Add(New ParserCase With {
                    .Name = "RandomCase_" & i.ToString(CultureInfo.InvariantCulture),
                    .Value = CreateRandomValue(rng, depth:=0, maxDepth:=4)
                })
            Next

            Dim passed As Integer = 0
            Dim failed As Integer = 0

            For i As Integer = 0 To cases.Count - 1

                Dim c As ParserCase = cases(i)

                Console.WriteLine()
                Console.WriteLine($"[{i + 1}/{cases.Count}] Case: {c.Name}")

                Try
                    Dim json As String = JsonConvert.SerializeObject(c.Value, Formatting.None)

                    Dim expectedJ As JToken = ParseNewtonsoft(json)
                    Dim parsed1 As BToken = BToken.ParseJSON(json)

                    ' 1) Parser original vs Newtonsoft
                    CompareJTokenToBToken(expectedJ, parsed1, "$")

                    ' 2) Encode LINQ -> Decode
                    Dim bytes1 As Byte() = EncodeToken(parsed1)
                    Dim decoded1 As BToken = DecodeToken(bytes1)

                    CompareBTokenToBToken(parsed1, decoded1, "$")
                    CompareJTokenToBToken(expectedJ, decoded1, "$")

                    ' 3) Re-encode decoded token -> decode novamente
                    Dim bytes2 As Byte() = EncodeToken(decoded1)
                    Dim decoded2 As BToken = DecodeToken(bytes2)

                    CompareBTokenToBToken(parsed1, decoded2, "$")
                    CompareBTokenToBToken(decoded1, decoded2, "$")
                    CompareJTokenToBToken(expectedJ, decoded2, "$")

                    ' 4) JSON redundante também
                    Dim jsonRound1 As String = decoded1.ToJson(False)
                    Dim jsonRound2 As String = decoded2.ToJson(False)

                    Dim jRound1 As JToken = ParseNewtonsoft(jsonRound1)
                    Dim jRound2 As JToken = ParseNewtonsoft(jsonRound2)

                    CompareJTokenToJToken(expectedJ, jRound1, "$")
                    CompareJTokenToJToken(expectedJ, jRound2, "$")
                    CompareJTokenToJToken(jRound1, jRound2, "$")

                    Console.ForegroundColor = ConsoleColor.Green
                    Console.WriteLine("PASS")
                    Console.ResetColor()

                    passed += 1

                Catch ex As Exception
                    Console.ForegroundColor = ConsoleColor.Red
                    Console.WriteLine("FAIL")
                    Console.WriteLine(ex.ToString())
                    Console.ResetColor()

                    failed += 1
                End Try

            Next

            Console.WriteLine()
            Console.WriteLine("======================================")
            Console.WriteLine($"Parser suite finished. Passed: {passed} | Failed: {failed}")
            Console.WriteLine("======================================")
            Console.WriteLine()

            If failed > 0 Then
                Throw New Exception($"Parser suite failed. Passed={passed}, Failed={failed}.")
            End If

        End Sub

#End Region

#Region "Encode / Decode helpers"

        Private Shared Function EncodeToken(token As BToken) As Byte()
            Return Bytery.Encode(token)
        End Function

        Private Shared Function DecodeToken(bytes As Byte()) As BToken
            Return Bytery.Decode(bytes)
        End Function

#End Region

#Region "Newtonsoft Control Parser"

        Private Shared Function ParseNewtonsoft(json As String) As JToken

            Using sr As New StringReader(json)
                Using reader As New JsonTextReader(sr)

                    reader.DateParseHandling = DateParseHandling.None
                    reader.FloatParseHandling = FloatParseHandling.Double

                    Return JToken.ReadFrom(reader)

                End Using
            End Using

        End Function

#End Region

#Region "Deep Compare - JToken vs BToken"

        Public Shared Sub CompareJTokenToBToken(expected As JToken, actual As BToken, path As String)

            If expected Is Nothing Then
                If actual IsNot Nothing AndAlso Not actual.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected null token reference, got {DescribeBToken(actual)}.")
                End If
                Return
            End If

            If expected.Type = JTokenType.Null Then
                If actual Is Nothing OrElse Not actual.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected null, got {DescribeBToken(actual)}.")
                End If
                Return
            End If

            If actual Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected {expected.Type}, got Nothing.")
            End If

            Select Case expected.Type

                Case JTokenType.Object
                    CompareObject(DirectCast(expected, JObject), actual, path)

                Case JTokenType.Array
                    CompareArray(DirectCast(expected, JArray), actual, path)

                Case JTokenType.String
                    CompareString(DirectCast(expected, JValue), actual, path)

                Case JTokenType.Integer
                    CompareNumber(DirectCast(expected, JValue), actual, path)

                Case JTokenType.Float
                    CompareNumber(DirectCast(expected, JValue), actual, path)

                Case JTokenType.Boolean
                    CompareBoolean(DirectCast(expected, JValue), actual, path)

                Case Else
                    Throw New Exception($"Unsupported JTokenType at {path}: {expected.Type}")

            End Select

        End Sub

        Private Shared Sub CompareObject(expected As JObject, actual As BToken, path As String)

            Dim obj As BObject = TryCast(actual, BObject)
            If obj Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected Object, got {DescribeBToken(actual)}.")
            End If

            If expected.Count <> obj.Count Then
                Throw New Exception($"Mismatch at {path}: expected object count {expected.Count}, got {obj.Count}.")
            End If

            For Each p As JProperty In expected.Properties()

                Dim child As BToken = Nothing
                If Not obj.TryGetDirectValue(p.Name, child) Then
                    Throw New Exception($"Mismatch at {path}: missing property ""{p.Name}"".")
                End If

                CompareJTokenToBToken(p.Value, child, path & "." & p.Name)

            Next

        End Sub

        Private Shared Sub CompareArray(expected As JArray, actual As BToken, path As String)

            Dim arr As BArray = TryCast(actual, BArray)
            If arr Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected Array, got {DescribeBToken(actual)}.")
            End If

            If expected.Count <> arr.Count Then
                Throw New Exception($"Mismatch at {path}: expected array count {expected.Count}, got {arr.Count}.")
            End If

            For i As Integer = 0 To expected.Count - 1
                CompareJTokenToBToken(expected(i), arr(i), path & "[" & i.ToString(CultureInfo.InvariantCulture) & "]")
            Next

        End Sub

        Private Shared Sub CompareString(expected As JValue, actual As BToken, path As String)

            Dim s As BString = TryCast(actual, BString)
            If s Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected String, got {DescribeBToken(actual)}.")
            End If

            Dim expectedValue As String = expected.Value(Of String)()
            Dim actualValue As String = s.Value

            If Not String.Equals(expectedValue, actualValue, StringComparison.Ordinal) Then
                Throw New Exception($"Mismatch at {path}: expected String ""{expectedValue}"", got ""{actualValue}"".")
            End If

        End Sub

        Private Shared Sub CompareBoolean(expected As JValue, actual As BToken, path As String)

            Dim b As BBoolean = TryCast(actual, BBoolean)
            If b Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected Boolean, got {DescribeBToken(actual)}.")
            End If

            Dim expectedValue As Boolean = expected.Value(Of Boolean)()
            Dim actualValue As Boolean = b.Value.GetValueOrDefault()

            If expectedValue <> actualValue Then
                Throw New Exception($"Mismatch at {path}: expected Boolean {expectedValue}, got {actualValue}.")
            End If

        End Sub

        Private Shared Sub CompareNumber(expected As JValue, actual As BToken, path As String)

            Dim n As BNumber = TryCast(actual, BNumber)
            If n Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected Number, got {DescribeBToken(actual)}.")
            End If

            Dim expectedValue As Double = Convert.ToDouble(expected.Value(Of Object)(), CultureInfo.InvariantCulture)
            Dim actualValue As Double = n.Value

            If expectedValue <> actualValue Then
                Throw New Exception(
                $"Mismatch at {path}: expected Number {expectedValue.ToString("R", CultureInfo.InvariantCulture)}, " &
                $"got {actualValue.ToString("R", CultureInfo.InvariantCulture)}.")
            End If

        End Sub

#End Region

#Region "Deep Compare - BToken vs BToken"

        Public Shared Sub CompareBTokenToBToken(expected As BToken, actual As BToken, path As String)

            If expected Is Nothing Then
                If actual IsNot Nothing AndAlso Not actual.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected Nothing/null, got {DescribeBToken(actual)}.")
                End If
                Return
            End If

            If expected.IsNull Then
                If actual Is Nothing OrElse Not actual.IsNull Then
                    Throw New Exception($"Mismatch at {path}: expected null, got {DescribeBToken(actual)}.")
                End If
                Return
            End If

            If actual Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected {DescribeBToken(expected)}, got Nothing.")
            End If

            If actual.IsNull Then
                Throw New Exception($"Mismatch at {path}: expected {DescribeBToken(expected)}, got null.")
            End If

            If TypeOf expected Is BObject Then
                CompareBObjectToBObject(DirectCast(expected, BObject), actual, path)
                Return
            End If

            If TypeOf expected Is BArray Then
                CompareBArrayToBArray(DirectCast(expected, BArray), actual, path)
                Return
            End If

            If TypeOf expected Is BString Then
                Dim a As BString = TryCast(actual, BString)
                If a Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected String, got {DescribeBToken(actual)}.")
                End If

                If Not String.Equals(DirectCast(expected, BString).Value, a.Value, StringComparison.Ordinal) Then
                    Throw New Exception($"Mismatch at {path}: expected String ""{DirectCast(expected, BString).Value}"", got ""{a.Value}"".")
                End If

                Return
            End If

            If TypeOf expected Is BBoolean Then
                Dim a As BBoolean = TryCast(actual, BBoolean)
                If a Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected Boolean, got {DescribeBToken(actual)}.")
                End If

                If DirectCast(expected, BBoolean).Value.GetValueOrDefault() <> a.Value.GetValueOrDefault() Then
                    Throw New Exception($"Mismatch at {path}: expected Boolean {DirectCast(expected, BBoolean).Value.GetValueOrDefault()}, got {a.Value.GetValueOrDefault()}.")
                End If

                Return
            End If

            If TypeOf expected Is BNumber Then
                Dim a As BNumber = TryCast(actual, BNumber)
                If a Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected Number, got {DescribeBToken(actual)}.")
                End If

                Dim ev As Double = DirectCast(expected, BNumber).Value
                Dim av As Double = a.Value

                If ev <> av Then
                    Throw New Exception(
                        $"Mismatch at {path}: expected Number {ev.ToString("R", CultureInfo.InvariantCulture)}, " &
                        $"got {av.ToString("R", CultureInfo.InvariantCulture)}.")
                End If

                Return
            End If

            If TypeOf expected Is BDate Then
                Dim a As BDate = TryCast(actual, BDate)
                If a Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected Date, got {DescribeBToken(actual)}.")
                End If

                If DirectCast(expected, BDate).Value <> a.Value Then
                    Throw New Exception($"Mismatch at {path}: expected Date {DirectCast(expected, BDate).Value:o}, got {a.Value:o}.")
                End If

                Return
            End If

            If TypeOf expected Is BBytes Then
                Dim a As BBytes = TryCast(actual, BBytes)
                If a Is Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected Bytes, got {DescribeBToken(actual)}.")
                End If

                If Not ByteArraysEqual(DirectCast(expected, BBytes).Value, a.Value) Then
                    Throw New Exception($"Mismatch at {path}: expected Bytes do not match actual Bytes.")
                End If

                Return
            End If

            Throw New Exception($"Unsupported BToken comparison at {path}: expected={expected.GetType().FullName}, actual={actual.GetType().FullName}.")

        End Sub

        Private Shared Sub CompareBObjectToBObject(expected As BObject, actual As BToken, path As String)

            Dim obj As BObject = TryCast(actual, BObject)
            If obj Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected Object, got {DescribeBToken(actual)}.")
            End If

            If expected.Count <> obj.Count Then
                Throw New Exception($"Mismatch at {path}: expected object count {expected.Count}, got {obj.Count}.")
            End If

            For Each kv As KeyValuePair(Of String, BToken) In expected

                Dim child As BToken = Nothing
                If Not obj.TryGetDirectValue(kv.Key, child) Then
                    Throw New Exception($"Mismatch at {path}: missing property ""{kv.Key}"".")
                End If

                CompareBTokenToBToken(kv.Value, child, path & "." & kv.Key)

            Next

        End Sub

        Private Shared Sub CompareBArrayToBArray(expected As BArray, actual As BToken, path As String)

            Dim arr As BArray = TryCast(actual, BArray)
            If arr Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected Array, got {DescribeBToken(actual)}.")
            End If

            If expected.Count <> arr.Count Then
                Throw New Exception($"Mismatch at {path}: expected array count {expected.Count}, got {arr.Count}.")
            End If

            For i As Integer = 0 To expected.Count - 1
                CompareBTokenToBToken(expected(i), arr(i), path & "[" & i.ToString(CultureInfo.InvariantCulture) & "]")
            Next

        End Sub

        Private Shared Function ByteArraysEqual(a As Byte(), b As Byte()) As Boolean

            If a Is Nothing Then Return b Is Nothing
            If b Is Nothing Then Return False
            If a.Length <> b.Length Then Return False

            For i As Integer = 0 To a.Length - 1
                If a(i) <> b(i) Then Return False
            Next

            Return True

        End Function

#End Region

#Region "Deep Compare - JToken vs JToken"

        Public Shared Sub CompareJTokenToJToken(expected As JToken, actual As JToken, path As String)

            If expected Is Nothing Then
                If actual IsNot Nothing Then
                    Throw New Exception($"Mismatch at {path}: expected Nothing, got {actual.Type}.")
                End If
                Return
            End If

            If actual Is Nothing Then
                Throw New Exception($"Mismatch at {path}: expected {expected.Type}, got Nothing.")
            End If

            If expected.Type <> actual.Type Then
                Throw New Exception($"Mismatch at {path}: expected JTokenType {expected.Type}, got {actual.Type}.")
            End If

            Select Case expected.Type

                Case JTokenType.Object
                    Dim eo As JObject = DirectCast(expected, JObject)
                    Dim ao As JObject = DirectCast(actual, JObject)

                    If eo.Count <> ao.Count Then
                        Throw New Exception($"Mismatch at {path}: expected object count {eo.Count}, got {ao.Count}.")
                    End If

                    For Each p As JProperty In eo.Properties()
                        Dim ap As JToken = ao(p.Name)
                        If ap Is Nothing Then
                            Throw New Exception($"Mismatch at {path}: missing property ""{p.Name}"".")
                        End If

                        CompareJTokenToJToken(p.Value, ap, path & "." & p.Name)
                    Next

                Case JTokenType.Array
                    Dim ea As JArray = DirectCast(expected, JArray)
                    Dim aa As JArray = DirectCast(actual, JArray)

                    If ea.Count <> aa.Count Then
                        Throw New Exception($"Mismatch at {path}: expected array count {ea.Count}, got {aa.Count}.")
                    End If

                    For i As Integer = 0 To ea.Count - 1
                        CompareJTokenToJToken(ea(i), aa(i), path & "[" & i.ToString(CultureInfo.InvariantCulture) & "]")
                    Next

                Case JTokenType.Null
                    Return

                Case JTokenType.String
                    Dim es As String = DirectCast(expected, JValue).Value(Of String)()
                    Dim acs As String = DirectCast(actual, JValue).Value(Of String)()

                    If Not String.Equals(es, acs, StringComparison.Ordinal) Then
                        Throw New Exception($"Mismatch at {path}: expected String ""{es}"", got ""{acs}"".")
                    End If

                Case JTokenType.Boolean
                    Dim eb As Boolean = DirectCast(expected, JValue).Value(Of Boolean)()
                    Dim ab As Boolean = DirectCast(actual, JValue).Value(Of Boolean)()

                    If eb <> ab Then
                        Throw New Exception($"Mismatch at {path}: expected Boolean {eb}, got {ab}.")
                    End If

                Case JTokenType.Integer, JTokenType.Float
                    Dim en As Double = Convert.ToDouble(DirectCast(expected, JValue).Value(Of Object)(), CultureInfo.InvariantCulture)
                    Dim an As Double = Convert.ToDouble(DirectCast(actual, JValue).Value(Of Object)(), CultureInfo.InvariantCulture)

                    If en <> an Then
                        Throw New Exception(
                            $"Mismatch at {path}: expected Number {en.ToString("R", CultureInfo.InvariantCulture)}, " &
                            $"got {an.ToString("R", CultureInfo.InvariantCulture)}.")
                    End If

                Case Else
                    Throw New Exception($"Unsupported JTokenType at {path}: {expected.Type}")

            End Select

        End Sub

#End Region

#Region "Describe"

        Private Shared Function DescribeBToken(token As BToken) As String

            If token Is Nothing Then Return "Nothing"

            If token.IsNull Then
                Return token.GetType().Name & "(null)"
            End If

            Return token.GetType().Name & "(" & token.ToString() & ")"

        End Function

#End Region

#Region "Fixed Cases"

        Private Shared Function BuildFixedCases() As List(Of ParserCase)

            Dim cases As New List(Of ParserCase)

            cases.Add(New ParserCase With {
                .Name = "NullRoot",
                .Value = Nothing
            })

            cases.Add(New ParserCase With {
                .Name = "BooleanRoot_True",
                .Value = True
            })

            cases.Add(New ParserCase With {
                .Name = "BooleanRoot_False",
                .Value = False
            })

            cases.Add(New ParserCase With {
                .Name = "IntegerRoot",
                .Value = 123456
            })

            cases.Add(New ParserCase With {
                .Name = "NegativeIntegerRoot",
                .Value = -987654
            })

            cases.Add(New ParserCase With {
                .Name = "FloatRoot",
                .Value = 1234.5678R
            })

            cases.Add(New ParserCase With {
                .Name = "StringRoot_Empty",
                .Value = ""
            })

            cases.Add(New ParserCase With {
                .Name = "StringRoot_Escapes",
                .Value = "John ""Doe""" & vbCrLf & "Tab:" & vbTab & " Slash:\ Back:/"
            })

            cases.Add(New ParserCase With {
                .Name = "StringRoot_Unicode",
                .Value = "Olá 漢字 ☃ Привет"
            })

            cases.Add(New ParserCase With {
                .Name = "ArrayRoot_Primitives",
                .Value = New Object() {1, 2.5R, True, False, Nothing, "text"}
            })

            cases.Add(New ParserCase With {
                .Name = "ObjectRoot_Simple",
                .Value = New Dictionary(Of String, Object) From {
                    {"id", 1},
                    {"name", "John"},
                    {"active", True},
                    {"score", 99.5R},
                    {"nothing", Nothing}
                }
            })

            cases.Add(New ParserCase With {
                .Name = "ObjectRoot_Nested",
                .Value = New Dictionary(Of String, Object) From {
                    {"client", New Dictionary(Of String, Object) From {
                        {"id", 7},
                        {"name", "Alice"},
                        {"pets", New List(Of Object) From {
                            New Dictionary(Of String, Object) From {
                                {"name", "Toby"},
                                {"age", 11}
                            },
                            New Dictionary(Of String, Object) From {
                                {"name", "Mia"},
                                {"age", 4}
                            }
                        }}
                    }},
                    {"ok", True}
                }
            })

            cases.Add(New ParserCase With {
                .Name = "ArrayRoot_Nested",
                .Value = New List(Of Object) From {
                    New Dictionary(Of String, Object) From {
                        {"a", 1},
                        {"b", New List(Of Object) From {1, 2, 3}}
                    },
                    New Dictionary(Of String, Object) From {
                        {"x", "hello"},
                        {"y", Nothing}
                    }
                }
            })

            cases.Add(New ParserCase With {
                .Name = "NumericEdge_Safe53",
                .Value = New Dictionary(Of String, Object) From {
                    {"maxSafe", 9007199254740991L},
                    {"minSafe", -9007199254740991L},
                    {"smallFloat", 0.000001R},
                    {"bigFloat", 1234567890.125R}
                }
            })

            Return cases

        End Function

#End Region

#Region "Random Generator"

        Private Shared Function CreateRandomValue(rng As Random,
                                                  depth As Integer,
                                                  maxDepth As Integer) As Object

            If depth >= maxDepth Then
                Return CreateRandomPrimitive(rng)
            End If

            Dim kind As Integer = rng.Next(0, 100)

            If kind < 45 Then
                Return CreateRandomPrimitive(rng)
            End If

            If kind < 72 Then
                Return CreateRandomArray(rng, depth + 1, maxDepth)
            End If

            Return CreateRandomObject(rng, depth + 1, maxDepth)

        End Function

        Private Shared Function CreateRandomPrimitive(rng As Random) As Object

            Select Case rng.Next(0, 6)

                Case 0
                    Return Nothing

                Case 1
                    Return (rng.Next(0, 2) = 0)

                Case 2
                    Return CLng(rng.Next(-1000000, 1000001))

                Case 3
                    Dim whole As Integer = rng.Next(-100000, 100001)
                    Dim frac As Double = rng.NextDouble()
                    Return whole + frac

                Case 4
                    Return CreateRandomString(rng)

                Case Else
                    Return rng.Next(-10, 11).ToString(CultureInfo.InvariantCulture)

            End Select

        End Function

        Private Shared Function CreateRandomArray(rng As Random,
                                                  depth As Integer,
                                                  maxDepth As Integer) As Object

            Dim count As Integer = rng.Next(0, 6)
            Dim list As New List(Of Object)(count)

            For i As Integer = 0 To count - 1
                list.Add(CreateRandomValue(rng, depth, maxDepth))
            Next

            Return list

        End Function

        Private Shared Function CreateRandomObject(rng As Random,
                                                   depth As Integer,
                                                   maxDepth As Integer) As Object

            Dim count As Integer = rng.Next(0, 6)
            Dim dict As New Dictionary(Of String, Object)(StringComparer.Ordinal)

            For i As Integer = 0 To count - 1

                Dim key As String
                Do
                    key = "k" & i.ToString(CultureInfo.InvariantCulture) & "_" & CreateRandomIdentifier(rng)
                Loop While dict.ContainsKey(key)

                dict.Add(key, CreateRandomValue(rng, depth, maxDepth))

            Next

            Return dict

        End Function

        Private Shared Function CreateRandomIdentifier(rng As Random) As String

            Dim len As Integer = rng.Next(3, 10)
            Dim chars(len - 1) As Char

            For i As Integer = 0 To len - 1
                Dim n As Integer = rng.Next(0, 36)
                chars(i) = If(n < 26, ChrW(AscW("a"c) + n), ChrW(AscW("0"c) + (n - 26)))
            Next

            Return New String(chars)

        End Function

        Private Shared Function CreateRandomString(rng As Random) As String

            Select Case rng.Next(0, 8)

                Case 0 : Return ""
                Case 1 : Return "simple"
                Case 2 : Return "with space"
                Case 3 : Return "quote "" here"
                Case 4 : Return "slash \ and /"
                Case 5 : Return "line1" & vbCrLf & "line2"
                Case 6 : Return "tab" & vbTab & "end"
                Case Else : Return "unicode " & ChrW(&H2603) & " " & "漢字"

            End Select

        End Function

#End Region

    End Class

End Namespace