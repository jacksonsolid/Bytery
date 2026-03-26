Option Explicit On
Option Strict On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text

Namespace Benchmark.Tests

    ''' <summary>
    ''' Wire-format boundary coverage:
    '''  - TagLen length cutovers: 230/231, 486/487, 65535/65536 (strings + bytes)
    '''  - TagLen array-length cutovers: 230/231, 486/487
    '''  - String pointer cutover: 255 -> 256 (forces TAGLEN_STRING_PTR_2B_TAG)
    '''  - Schema pointer cutover: 255 -> 256 (forces TAGLEN_SCHEMA_PTR_2B_TAG via many distinct schemas + overrides)
    '''  - IntTag cutovers: 219/220, 475/476, -19/-20, -275/-276, Min/Max, + null sentinel (via Nullable)
    ''' </summary>
    Friend Class WireFormatBoundaries
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "WireFormatBoundaries"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Boundary coverage for TagLen/IntTag and pointer-size transitions (1B->2B) across strings, bytes, arrays, and schema pointers."
            End Get
        End Property

        ' Esse teste é mais “corretude/boundaries” do que throughput:
        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 1
            End Get
        End Property

        ' Recomendo 1 thread aqui, porque o payload é grande e o objetivo é cobrir bordas.
        Public Overrides ReadOnly Property Threads As Integer
            Get
                Return 1
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            Dim root As New Dictionary(Of String, Object)(StringComparer.Ordinal)

            ' ----------------------------
            ' 1) IntTag boundaries (as Object map values)
            ' ----------------------------
            root("i219") = 219
            root("i220") = 220
            root("i475") = 475
            root("i476") = 476

            root("n19") = -19
            root("n20") = -20
            root("n275") = -275
            root("n276") = -276

            root("longMin") = Long.MinValue
            root("longMax") = Long.MaxValue

            ' ----------------------------
            ' 2) TagLen string length boundaries (literal strings; don't repeat these)
            ' ----------------------------
            root("s0") = ""
            root("s1") = "A"
            root("s230") = New String("a"c, 230)
            root("s231") = New String("b"c, 231)
            root("s486") = New String("c"c, 486)
            root("s487") = New String("d"c, 487)
            root("s65535") = New String("e"c, 65535)   ' forces TAGLEN_LEN_U16_TAG path
            root("s65536") = New String("f"c, 65536)   ' forces TAGLEN_LEN_U24_TAG path

            ' ----------------------------
            ' 3) TagLen bytes length boundaries (literal bytes; don't repeat)
            ' ----------------------------
            root("b0") = Array.Empty(Of Byte)()
            root("b1") = New Byte() {&H2A}
            root("b230") = MakeBytes(230)
            root("b231") = MakeBytes(231)
            root("b486") = MakeBytes(486)
            root("b487") = MakeBytes(487)
            root("b65535") = MakeBytes(65535)
            root("b65536") = MakeBytes(65536)

            ' ----------------------------
            ' 4) TagLen array-length boundaries (array counts use Converter.LengthTag)
            ' ----------------------------
            root("arrI230") = MakeIntArray(230)
            root("arrI231") = MakeIntArray(231)
            root("arrI486") = MakeIntArray(486)
            root("arrI487") = MakeIntArray(487)

            root("arrS230") = MakeStringArrayFixedLen(230, "x")
            root("arrS231") = MakeStringArrayFixedLen(231, "y")
            root("arrB230") = MakeBoolArray(230)
            root("arrB231") = MakeBoolArray(231)

            ' ----------------------------
            ' 5) Force string-table pointers to cross 255->256 (TAGLEN_STRING_PTR_2B_TAG)
            '    Need >=257 cached entries. Each distinct string must appear twice.
            ' ----------------------------
            root("repeatStrings") = BuildRepeatedStringsPointerStorm(uniqueCount:=260)

            ' ----------------------------
            ' 6) Force schema pointer to cross 255->256 (TAGLEN_SCHEMA_PTR_2B_TAG)
            '    We add many distinct runtime OBJECT schemas inside an Object-typed map:
            '      - each value is a different ValueTuple(Of T1,T2,T3,T4) signature
            '      - declared expected schema is System.Object => schema overrides everywhere
            '      - after >256 distinct schemas, override pointers become 2-byte payload
            ' ----------------------------
            Dim schemaStorm As New Dictionary(Of String, Object)(capacity:=320, comparer:=StringComparer.Ordinal)
            For i As Integer = 0 To 299
                schemaStorm("vt" & i.ToString("D3", CultureInfo.InvariantCulture)) = MakeTuple4Variant(i)
            Next
            root("schemaStorm") = schemaStorm

            ' ----------------------------
            ' 7) Typed maps to force nullable NULL sentinels (IntTag null, Float null, Bool null, Date null)
            ' ----------------------------
            root("typedIntMap") = New Dictionary(Of String, Long?)(StringComparer.Ordinal) From {
                {"a", 0L},
                {"b", 1L},
                {"c", Nothing},          ' INTTAG_NULL_TAG
                {"d", 219L},
                {"e", 220L},
                {"f", 475L},
                {"g", 476L},
                {"h", -19L},
                {"i", -20L},
                {"j", -275L},
                {"k", -276L},
                {"min", Long.MinValue},
                {"max", Long.MaxValue}
            }

            root("typedDoubleMap") = New Dictionary(Of String, Double?)(StringComparer.Ordinal) From {
                {"a", 0.0R},
                {"b", 1.25R},
                {"c", Nothing},                 ' FLOAT8_NULL_PAYLOAD_BE
                {"nan", Double.NaN},
                {"posInf", Double.PositiveInfinity},
                {"negInf", Double.NegativeInfinity}
            }

            root("typedBoolMap") = New Dictionary(Of String, Boolean?)(StringComparer.Ordinal) From {
                {"t", True},
                {"f", False},
                {"n", Nothing}                  ' BOOL_PAYLOAD_NULL
            }

            root("typedDateMap") = New Dictionary(Of String, DateTime?)(StringComparer.Ordinal) From {
                {"t0", Utc(2025, 1, 1, 0, 0, 0)},
                {"t1", Utc(2025, 12, 31, 23, 59, 59)},
                {"n", Nothing}                  ' DATE_NULL_PAYLOAD
            }

            ' ----------------------------
            ' Return multiple roots (opcional, mas bom pra cobrir root-map e root-object)
            '  - root: Dictionary(Of String,Object) (map root)
            '  - primitives: strong typed object root (object root)
            ' ----------------------------
            Dim primitives As New BoundaryPrimitives With {
                .Id = "B-" & GuidLikeHex(16),
                .CreatedUtc = Utc(2025, 2, 2, 2, 2, 2),
                .Ints = New Long?() {219L, 220L, 475L, 476L, -19L, -20L, -275L, -276L, Nothing, Long.MinValue, Long.MaxValue},
                .Doubles = New Double?() {0.1R, Nothing, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity, 123456.789R},
                .Bools = New Boolean?() {True, False, Nothing, True},
                .Dates = New DateTime?() {Utc(2025, 1, 1, 0, 0, 0), Nothing, Utc(2025, 6, 1, 12, 34, 56)},
                .Bytes230 = MakeBytes(230),
                .Str231 = New String("Z"c, 231)
            }

            Return New List(Of Object) From {root, primitives}

        End Function

#Region "Generator helpers"

        Private Shared Function MakeBytes(len As Integer) As Byte()
            If len <= 0 Then Return Array.Empty(Of Byte)()
            Dim b(len - 1) As Byte
            For i As Integer = 0 To len - 1
                b(i) = CByte(i And &HFF)
            Next
            Return b
        End Function

        Private Shared Function MakeIntArray(len As Integer) As Integer()
            If len <= 0 Then Return Array.Empty(Of Integer)()
            Dim a(len - 1) As Integer
            For i As Integer = 0 To len - 1
                ' mistura de valores pequenos/grandes e negativos
                If (i Mod 11) = 0 Then
                    a(i) = Integer.MinValue
                ElseIf (i Mod 13) = 0 Then
                    a(i) = Integer.MaxValue
                ElseIf (i Mod 7) = 0 Then
                    a(i) = -i
                Else
                    a(i) = i
                End If
            Next
            Return a
        End Function

        Private Shared Function MakeStringArrayFixedLen(len As Integer, prefix As String) As String()
            If len <= 0 Then Return Array.Empty(Of String)()
            Dim a(len - 1) As String
            For i As Integer = 0 To len - 1
                a(i) = prefix & i.ToString(CultureInfo.InvariantCulture)
            Next
            Return a
        End Function

        Private Shared Function MakeBoolArray(len As Integer) As Boolean()
            If len <= 0 Then Return Array.Empty(Of Boolean)()
            Dim a(len - 1) As Boolean
            For i As Integer = 0 To len - 1
                a(i) = ((i And 1) = 0)
            Next
            Return a
        End Function

        Private Shared Function BuildRepeatedStringsPointerStorm(uniqueCount As Integer) As List(Of String)
            ' Each string appears twice => 2nd occurrence becomes string-table entry (pointer).
            Dim u As Integer = Math.Max(0, uniqueCount)
            Dim list As New List(Of String)(u * 2 + 8)

            For i As Integer = 0 To u - 1
                list.Add("ptr-" & i.ToString("D4", CultureInfo.InvariantCulture))
            Next

            For i As Integer = 0 To u - 1
                list.Add("ptr-" & i.ToString("D4", CultureInfo.InvariantCulture))
            Next

            ' extra hits to ensure "pointerByValue" fast-path too
            If u > 0 Then
                list.Add("ptr-0000")
                list.Add("ptr-0001")
                list.Add("ptr-0000")
            End If

            Return list
        End Function

        Private Shared ReadOnly _catTypes As Type() = {
            GetType(Integer),
            GetType(String),
            GetType(Double),
            GetType(Boolean),
            GetType(DateTime),
            GetType(Byte()),
            GetType(DummyObj)
        }

        Private Shared Function MakeTuple4Variant(i As Integer) As Object
            ' Unique pattern by base-7 digits (arity 4 => 7^4=2401 patterns; we use first 300)
            Dim a As Integer = (i Mod 7)
            Dim b As Integer = ((i \ 7) Mod 7)
            Dim c As Integer = ((i \ 49) Mod 7)
            Dim d As Integer = ((i \ 343) Mod 7)

            Dim t1 As Type = _catTypes(a)
            Dim t2 As Type = _catTypes(b)
            Dim t3 As Type = _catTypes(c)
            Dim t4 As Type = _catTypes(d)

            Dim tupleType As Type = GetType(ValueTuple(Of , , ,)).MakeGenericType(t1, t2, t3, t4)

            Dim args As Object() = {
                MakeValueForCategory(t1, i, 1),
                MakeValueForCategory(t2, i, 2),
                MakeValueForCategory(t3, i, 3),
                MakeValueForCategory(t4, i, 4)
            }

            Return Activator.CreateInstance(tupleType, args)
        End Function

        Private Shared Function MakeValueForCategory(t As Type, i As Integer, slot As Integer) As Object
            If t Is GetType(Integer) Then
                Return i * 17 + slot
            End If

            If t Is GetType(String) Then
                Return "t" & slot.ToString(CultureInfo.InvariantCulture) & "-s-" & i.ToString("D4", CultureInfo.InvariantCulture)
            End If

            If t Is GetType(Double) Then
                Return CDbl(i) + (slot * 0.25R)
            End If

            If t Is GetType(Boolean) Then
                Return ((i + slot) And 1) = 0
            End If

            If t Is GetType(DateTime) Then
                Return New DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i + (slot * 1000))
            End If

            If t Is GetType(Byte()) Then
                Dim n As Integer = 8 + (i Mod 24)
                Dim b(n - 1) As Byte
                For k As Integer = 0 To n - 1
                    b(k) = CByte((i + slot + k) And &HFF)
                Next
                Return b
            End If

            If t Is GetType(DummyObj) Then
                Return New DummyObj(i, slot)
            End If

            ' fallback defensivo
            Return Nothing
        End Function

        Private Shared Function GuidLikeHex(len As Integer) As String
            Const chars As String = "abcdef0123456789"
            Dim r As New Random(12345)
            Dim sb As New StringBuilder(len)
            For i As Integer = 1 To len
                sb.Append(chars(r.Next(0, chars.Length)))
            Next
            Return sb.ToString()
        End Function

        Private Shared Function Utc(y As Integer, m As Integer, d As Integer, hh As Integer, mm As Integer, ss As Integer) As DateTime
            Return New DateTime(y, m, d, hh, mm, ss, DateTimeKind.Utc)
        End Function

#End Region

#Region "Templates"

        Private NotInheritable Class DummyObj
            Public Property Id As Integer
            Public Property Slot As Integer
            Public Property Name As String
            Public Property Payload As Byte()

            Public Sub New()
            End Sub

            Public Sub New(i As Integer, slot As Integer)
                Me.Id = i
                Me.Slot = slot
                Me.Name = "dummy-" & slot.ToString(CultureInfo.InvariantCulture) & "-" & i.ToString("D4", CultureInfo.InvariantCulture)
                Me.Payload = MakeBytes(16 + (i Mod 32))
            End Sub
        End Class

        Private NotInheritable Class BoundaryPrimitives
            Public Property Id As String
            Public Property CreatedUtc As DateTime

            Public Property Ints As Long?()
            Public Property Doubles As Double?()
            Public Property Bools As Boolean?()
            Public Property Dates As DateTime?()

            Public Property Bytes230 As Byte()
            Public Property Str231 As String
        End Class

#End Region

    End Class

End Namespace