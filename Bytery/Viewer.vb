Imports System.Globalization
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports Bytery.JSON

''' <summary>
''' Visual debugging utility for Bytery payloads.
''' </summary>
Friend NotInheritable Class Viewer

    Private Structure HexSpan
        Public Start As Integer
        Public [End] As Integer
        Public Fore As ConsoleColor
        Public Label As String
    End Structure

    Private NotInheritable Class SchemaDef
        Public Kind As JsonSchema.SchemaKind
        Public Fields() As FieldDef
        Public MapValueType As JsonFieldType = JsonFieldType.Unknown
        Public MapValueSchemaIndex As Integer = -1
        Public ArrayElemType As JsonFieldType = JsonFieldType.Unknown
        Public ArrayElemSchemaIndex As Integer = -1
    End Class

    Private Structure FieldDef
        Public Name As String
        Public TypeCode As JsonFieldType
        Public SchemaIndex As Integer
    End Structure

    Private _hexSpans As HexSpan() = Array.Empty(Of HexSpan)()
    Private _schemas As SchemaDef() = Array.Empty(Of SchemaDef)()
    Private _strings As String() = Array.Empty(Of String)()
    Private _dates As DateTime() = Array.Empty(Of DateTime)()
    Private _data As Byte()
    Private r As Decoding.Reader
    Private _fileVersion As Byte
    Private _zoneMask As Byte

    Private Sub New(data As Byte())
        _data = If(data, Array.Empty(Of Byte)())
        r = New Decoding.Reader(_data)
    End Sub

    Public Shared Sub Render(data As Byte())

        If data Is Nothing OrElse data.Length = 0 Then
            Console.WriteLine("(empty payload)")
            Return
        End If

        If IsGZip(data) Then
            data = DecompressGZip(data)
        End If

        Dim v As New Viewer(data)

        v.BuildHexSpans()

        Console.WriteLine()
        v.PrintHexLegend()
        v.PrintHexTable()

        Console.WriteLine("-------")
        v.ReadMagic()
        v.ReadZoneMask()

        If v.HasZone(ZMSK_HEADERS) Then
            v.ReadHeaders()
        Else
            v.PrintZoneOmitted("Headers")
        End If

        If v.HasZone(ZMSK_FILES) Then
            v.ReadFilesZone()
        Else
            v.PrintZoneOmitted("Files")
        End If

        If v.HasZone(ZMSK_STRING_TABLE) Then
            v.ReadStringTable()
        Else
            v.PrintZoneOmitted("String Table")
        End If

        If v.HasZone(ZMSK_DATE_TABLE) Then
            v.ReadDateTable()
        Else
            v.PrintZoneOmitted("Date Table")
        End If

        If v.HasZone(ZMSK_SCHEMA_TABLE) Then
            v.ReadSchemas()
        Else
            v.PrintZoneOmitted("Schemas")
        End If

        If v.HasZone(ZMSK_DATA) Then
            v.ReadData()
        Else
            v.PrintZoneOmitted("Data")
        End If

        If v.r.Remaining() <> 0 Then
            Console.WriteLine(":: Trailing ::")
            Print("remaining=" & v.r.Remaining().ToString(CultureInfo.InvariantCulture), ConsoleColor.Red)
            Console.WriteLine()
            Console.WriteLine()
        End If

        Console.WriteLine("-------")

    End Sub

    Public Sub PrintHexTable(Optional start As Integer = 0,
                             Optional length As Integer = -1,
                             Optional bytesPerRow As Integer = 16)

        If bytesPerRow <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(bytesPerRow))
        If start < 0 OrElse start > _data.Length Then Throw New ArgumentOutOfRangeException(NameOf(start))

        Dim endExclusive As Integer
        If length < 0 Then
            endExclusive = _data.Length
        Else
            Dim tmp As Long = CLng(start) + CLng(length)
            If tmp < start Then Throw New ArgumentOutOfRangeException(NameOf(length))
            endExclusive = CInt(Math.Min(tmp, CLng(_data.Length)))
        End If

        If start >= endExclusive Then
            Console.WriteLine("(empty range)")
            Return
        End If

        Console.Write("OFFSET    ")
        For i As Integer = 0 To bytesPerRow - 1
            Console.Write(i.ToString("X2"))
            Console.Write(" ")
        Next
        Console.Write(" | ")
        Console.WriteLine("VIEW")

        Dim oldColor As ConsoleColor = Console.ForegroundColor
        Dim rowOffset As Integer = start

        While rowOffset < endExclusive

            Dim rowCount As Integer = Math.Min(bytesPerRow, endExclusive - rowOffset)

            Console.ForegroundColor = ConsoleColor.DarkGray
            Console.Write(rowOffset.ToString("X8"))
            Console.Write("  ")

            For i As Integer = 0 To bytesPerRow - 1

                If i < rowCount Then
                    Dim off As Integer = rowOffset + i
                    Console.ForegroundColor = GetHexForeColor(off)
                    Console.Write(_data(off).ToString("X2"))
                Else
                    Console.ForegroundColor = ConsoleColor.Gray
                    Console.Write("  ")
                End If

                Console.Write(" ")

            Next

            Console.ForegroundColor = ConsoleColor.DarkGray
            Console.Write("| ")

            For i As Integer = 0 To bytesPerRow - 1

                If i < rowCount Then

                    Dim b As Byte = _data(rowOffset + i)

                    If b >= 32 AndAlso b <= 126 Then
                        Console.ForegroundColor = ConsoleColor.White
                        Console.Write(ChrW(b))
                        Console.Write(" "c)
                    Else
                        Console.ForegroundColor = ConsoleColor.DarkGray
                        Console.Write(b.ToString("X2"))
                    End If

                Else
                    Console.ForegroundColor = ConsoleColor.DarkGray
                    Console.Write("  ")
                End If

                Console.Write(" "c)

            Next

            Console.WriteLine()
            rowOffset += bytesPerRow

        End While

        Console.ForegroundColor = oldColor

    End Sub

    Private Shared Sub PrintBytes(bs As Byte(), Optional fore As ConsoleColor = ConsoleColor.Gray, Optional back As ConsoleColor = ConsoleColor.Black)
        For Each b As Byte In bs
            PrintByte(b, fore, back)
        Next
    End Sub

    Private Shared Sub PrintByte(b As Byte, Optional fore As ConsoleColor = ConsoleColor.Gray, Optional back As ConsoleColor = ConsoleColor.Black)
        Dim word As String = If(b < 31 OrElse b > 127, b.ToString("X2"), ChrW(b).ToString())
        Print(word, fore, back)
    End Sub

    Private Shared Sub Print(v As Long, Optional fore As ConsoleColor = ConsoleColor.Gray, Optional back As ConsoleColor = ConsoleColor.Black)
        Print(v.ToString(CultureInfo.InvariantCulture), fore, back)
    End Sub

    Private Shared Sub Print(word As String, Optional fore As ConsoleColor = ConsoleColor.Gray, Optional back As ConsoleColor = ConsoleColor.Black)

        Dim oldBack As ConsoleColor = Console.BackgroundColor
        Dim oldFore As ConsoleColor = Console.ForegroundColor

        Console.ForegroundColor = fore
        Console.BackgroundColor = back
        Console.Write(word & " ")

        Console.ForegroundColor = oldFore
        Console.BackgroundColor = oldBack

    End Sub

#Region "Container sections"

    Private Sub ReadMagic()

        Dim b0 As Byte = r.ReadByte()
        Dim b1 As Byte = r.ReadByte()
        Dim b2 As Byte = r.ReadByte()
        Dim b3 As Byte = r.ReadByte()

        If b0 <> FILE_MAGIC_B0 OrElse b1 <> FILE_MAGIC_B1 OrElse b2 <> FILE_MAGIC_B2 OrElse b3 <> FILE_MAGIC_B3 Then
            Throw New Exception($"Invalid file magic. Expected BYT1, got 0x{b0:X2} 0x{b1:X2} 0x{b2:X2} 0x{b3:X2}.")
        End If

        _fileVersion = r.ReadByte()

        If _fileVersion <> FILE_VERSION_V1 Then
            Throw New Exception("Unsupported file version: " & _fileVersion)
        End If

        Console.WriteLine(":: Magic ::")
        PrintBytes({b0, b1, b2, b3})
        Print("version=" & _fileVersion.ToString(CultureInfo.InvariantCulture), ConsoleColor.White)
        Console.WriteLine()
        Console.WriteLine()

    End Sub

    Private Sub ReadZoneMask()

        _zoneMask = r.ReadByte()

        Console.WriteLine(":: ZMSK ::")

        Print("0x" & _zoneMask.ToString("X2"), ConsoleColor.Black, ConsoleColor.White)

        If (_zoneMask And ZMSK_HAS_NEXT) <> 0 Then
            Print("HAS_NEXT=1 (unsupported in viewer)", ConsoleColor.Red)
            Console.WriteLine()
            Throw New Exception("Unsupported ZMSK chain in Viewer.")
        End If

        Dim unsupported As Integer =
            (CInt(_zoneMask) And Not CInt(ZMSK_V1_DEFINED_ZONES_MASK) And &HFF)

        If unsupported <> 0 Then
            Print("UNSUPPORTED=0x" & unsupported.ToString("X2"), ConsoleColor.Red)
            Console.WriteLine()
            Throw New Exception("Unsupported ZMSK bits in Viewer: 0x" & unsupported.ToString("X2"))
        End If

        Print("HEADERS=" & If(HasZone(ZMSK_HEADERS), "1", "0"), If(HasZone(ZMSK_HEADERS), ConsoleColor.Cyan, ConsoleColor.DarkGray))
        Print("FILES=" & If(HasZone(ZMSK_FILES), "1", "0"), If(HasZone(ZMSK_FILES), ConsoleColor.Blue, ConsoleColor.DarkGray))
        Print("STRINGS=" & If(HasZone(ZMSK_STRING_TABLE), "1", "0"), If(HasZone(ZMSK_STRING_TABLE), ConsoleColor.Red, ConsoleColor.DarkGray))
        Print("DATES=" & If(HasZone(ZMSK_DATE_TABLE), "1", "0"), If(HasZone(ZMSK_DATE_TABLE), ConsoleColor.DarkGreen, ConsoleColor.DarkGray))
        Print("SCHEMAS=" & If(HasZone(ZMSK_SCHEMA_TABLE), "1", "0"), If(HasZone(ZMSK_SCHEMA_TABLE), ConsoleColor.Magenta, ConsoleColor.DarkGray))
        Print("DATA=" & If(HasZone(ZMSK_DATA), "1", "0"), If(HasZone(ZMSK_DATA), ConsoleColor.Yellow, ConsoleColor.DarkGray))

        Console.WriteLine()
        Console.WriteLine()

    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function HasZone(bit As Byte) As Boolean
        Return (_zoneMask And bit) = bit
    End Function

    Private Sub PrintZoneOmitted(zoneName As String)
        Console.WriteLine(":: " & zoneName & " ::")
        Print("(omitted)", ConsoleColor.DarkGray)
        Console.WriteLine()
        Console.WriteLine()
    End Sub

    Private Sub ReadHeaders()

        Console.WriteLine(":: Headers ::")

        Dim headers As List(Of HeaderEntry) = r.ReadHeader()

        If headers Is Nothing Then
            Throw New Exception("HEADER zone is present in ZMSK, but Reader returned Nothing.")
        End If

        Print(headers.Count, ConsoleColor.Black, ConsoleColor.Cyan)
        Print("headers", ConsoleColor.Cyan)
        Console.WriteLine()

        If headers.Count = 0 Then
            Console.WriteLine()
            Return
        End If

        For i As Integer = 0 To headers.Count - 1
            Dim h As HeaderEntry = headers(i)
            Print("[" & i.ToString(CultureInfo.InvariantCulture) & "]", ConsoleColor.DarkGray)
            Print(h.Key, ConsoleColor.White)
            Print(h.TypeCode.ToString(), ConsoleColor.Cyan)
            Print(FormatHeaderValue(h.Value), ConsoleColor.Yellow)
            Console.WriteLine()
        Next

        Console.WriteLine()

    End Sub

    Private Sub ReadFilesZone()

        Console.WriteLine(":: Files ::")

        Dim declaredBodyLength As Integer =
        r.CheckedLength(r.ReadLUINT(), "Files body length")

        If declaredBodyLength < 1 Then
            Throw New Exception("FILES body length cannot be 0. It must include the serialized fileCount.")
        End If

        Dim startPos As Integer = r.Position()

        Dim count As Integer =
        r.CheckedCount(r.ReadLUINT(), 1, "Files count")

        If count = 0 Then
            Throw New Exception("FILES zone must be omitted when fileCount = 0.")
        End If

        Print(count, ConsoleColor.Black, ConsoleColor.Blue)
        Print("files", ConsoleColor.Blue)
        Print("bodyBytes=" & declaredBodyLength.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
        Console.WriteLine()

        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)

        For i As Integer = 0 To count - 1

            Dim fileName As String = ReadLstrLiteralNonNull("File name")

            If String.IsNullOrWhiteSpace(fileName) Then
                Throw New Exception("FILES zone cannot contain null, empty or whitespace file names.")
            End If

            If Not seen.Add(fileName) Then
                Throw New Exception("Duplicate file name in FILES zone: " & fileName)
            End If

            Dim isNull As Boolean
            Dim b() As Byte = r.ReadBarrOrNull(isNull)

            If isNull Then
                Throw New Exception("FILES zone cannot contain NULL file payloads.")
            End If

            Print("[" & i.ToString(CultureInfo.InvariantCulture) & "]", ConsoleColor.DarkGray)
            Print(fileName, ConsoleColor.White)
            Print("len=" & b.Length.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)

            Dim p As Integer = Math.Min(16, b.Length)
            If p > 0 Then
                Print("hex:", ConsoleColor.DarkGray)
                For j As Integer = 0 To p - 1
                    Print(b(j).ToString("X2"), ConsoleColor.DarkGray)
                Next
                If b.Length > p Then
                    Print("... (+" & (b.Length - p).ToString(CultureInfo.InvariantCulture) & ")", ConsoleColor.DarkGray)
                End If
            End If

            Console.WriteLine()

        Next

        Dim actualBodyLength As Integer = r.Position() - startPos

        If actualBodyLength <> declaredBodyLength Then
            Throw New Exception(
            $"FILES body length mismatch. Declared={declaredBodyLength}, actual={actualBodyLength}.")
        End If

        Console.WriteLine()

    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ReadLstrLiteralNonNull(label As String) As String

        Dim isNull As Boolean
        Dim len As Long = r.ReadLUINTOrNull(isNull)

        If isNull Then
            Throw New Exception(label & " cannot be null.")
        End If

        Return r.ReadUtf8Literal(r.CheckedLength(len, label & " length"))

    End Function

    Private Sub ReadStringTable()

        Console.WriteLine(":: String Table ::")

        Dim count As Integer = r.CheckedCount(r.ReadLUINT(), 1, "String table count")
        Print(count, ConsoleColor.Black, ConsoleColor.Red)

        If count = 0 Then
            _strings = Array.Empty(Of String)()
            Console.WriteLine()
            Return
        End If

        Dim table(count - 1) As String

        For i As Integer = 0 To count - 1

            Dim isNull As Boolean
            Dim len As Long = r.ReadLUINTOrNull(isNull)

            If isNull Then
                Throw New Exception("String table cannot contain NULL entries.")
            End If

            Dim s As String = r.ReadUtf8Literal(r.CheckedLength(len, "String table entry length"))
            table(i) = s

            Print(len, ConsoleColor.Red)
            Print(s, ConsoleColor.Yellow)

        Next

        _strings = table
        Console.WriteLine()

    End Sub

    Private Sub ReadDateTable()

        Console.WriteLine(":: Date Table ::")

        Dim count As Integer = r.CheckedCount(r.ReadLUINT(), 8, "Date table count")
        Print(count, ConsoleColor.Black, ConsoleColor.DarkGreen)

        If count = 0 Then
            _dates = Array.Empty(Of DateTime)()
            Console.WriteLine()
            Return
        End If

        Dim table(count - 1) As DateTime

        For i As Integer = 0 To count - 1

            Dim isNull As Boolean
            Dim dt As DateTime = r.ReadLDate(isNull)

            If isNull Then
                Throw New Exception("Date table cannot contain NULL entries.")
            End If

            table(i) = dt
            Print(dt.ToString("o"), ConsoleColor.White)

        Next

        _dates = table
        Console.WriteLine()

    End Sub

    Private Sub ReadSchemas()

        Console.WriteLine(":: Schemas ::")

        Dim schemaCount As Integer = r.CheckedCount(r.ReadLUINT(), 1, "Schema count")
        Print(schemaCount, ConsoleColor.Black, ConsoleColor.Magenta)
        Print("schemas", ConsoleColor.Magenta)
        Console.WriteLine()

        If schemaCount = 0 Then
            _schemas = Array.Empty(Of SchemaDef)()
            Console.WriteLine()
            Return
        End If

        Dim schemas(schemaCount - 1) As SchemaDef

        For i As Integer = 0 To schemaCount - 1

            Dim smat As Byte = r.ReadByte()

            Print("[" & i.ToString(CultureInfo.InvariantCulture) & "]", ConsoleColor.DarkGray)
            Print("SMAT=" & smat.ToString("X2"), ConsoleColor.Cyan)

            Select Case smat

                Case SMAT_NULL_TAG
                    Print("NULL", ConsoleColor.Green)
                    Console.WriteLine()
                    schemas(i) = Nothing

                Case SMAT_MAP_BASE_TAG To SMAT_ARR_MAX_TAG
                    schemas(i) = ReadNonObjectSchema(smat)

                Case Else
                    schemas(i) = ReadObjectSchema(smat)

            End Select

        Next

        _schemas = schemas
        Console.WriteLine()

    End Sub

    Private Function ReadNonObjectSchema(smat As Byte) As SchemaDef

        Dim s As New SchemaDef With {
            .Fields = Array.Empty(Of FieldDef)()
        }

        Dim typeId As Integer
        Dim isMapArr As Boolean = False

        Select Case smat

            Case SMAT_MAP_BASE_TAG To SMAT_MAP_MAX_TAG
                s.Kind = JsonSchema.SchemaKind.Map
                typeId = (CInt(smat) - CInt(SMAT_MAP_BASE_TAG)) + 1
                Print("MAP", ConsoleColor.Yellow)

            Case SMAT_MAP_ARR_BASE_TAG To SMAT_MAP_ARR_MAX_TAG
                s.Kind = JsonSchema.SchemaKind.Map
                isMapArr = True
                typeId = (CInt(smat) - CInt(SMAT_MAP_ARR_BASE_TAG)) + 1
                Print("MAP[]", ConsoleColor.Yellow)

            Case Else
                s.Kind = JsonSchema.SchemaKind.Array
                typeId = (CInt(smat) - CInt(SMAT_ARR_BASE_TAG)) + 1
                Print("ARRAY", ConsoleColor.Yellow)

        End Select

        If typeId < SMAT_TYPE_MIN_ID OrElse typeId > SMAT_TYPE_MAX_ID Then
            Throw New Exception("Invalid SMAT typeId: " & typeId)
        End If

        Dim baseType As JsonFieldType = CType(typeId, JsonFieldType)
        Dim schemaRef As Integer = -1

        Print(baseType.ToString(), ConsoleColor.White)

        If baseType = JsonFieldType.Object Then
            schemaRef = r.ReadSchemaPointerIndex()
            Print("->", ConsoleColor.DarkGray)
            Print("schema#" & schemaRef.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
        End If

        If s.Kind = JsonSchema.SchemaKind.Map Then
            s.MapValueType = If(isMapArr,
                                CType(CInt(baseType) Or CInt(JsonFieldType.ArrayFlag), JsonFieldType),
                                baseType)
            s.MapValueSchemaIndex = schemaRef
        Else
            s.ArrayElemType = baseType
            s.ArrayElemSchemaIndex = schemaRef
        End If

        Console.WriteLine()
        Return s

    End Function

    Private Function ReadObjectSchema(firstSmat As Byte) As SchemaDef

        Dim fieldCount As Integer = ReadSmatObjectFieldCountFromFirst(firstSmat)

        Print("OBJECT", ConsoleColor.Yellow)
        Print("fields=" & fieldCount.ToString(CultureInfo.InvariantCulture), ConsoleColor.White)
        Console.WriteLine()

        Dim s As New SchemaDef With {
            .Kind = JsonSchema.SchemaKind.Object
        }

        If fieldCount = 0 Then
            s.Fields = Array.Empty(Of FieldDef)()
            Console.WriteLine()
            Return s
        End If

        Dim fields(fieldCount - 1) As FieldDef

        For i As Integer = 0 To fieldCount - 1

            Dim typeCode As JsonFieldType = CType(r.ReadByte(), JsonFieldType)
            Dim fieldName As String = ReadFieldName()
            Dim schemaIndex As Integer = -1

            If BaseType(typeCode) = JsonFieldType.Object Then
                schemaIndex = r.ReadSchemaPointerIndex()
            End If

            fields(i) = New FieldDef With {
                .Name = fieldName,
                .TypeCode = typeCode,
                .SchemaIndex = schemaIndex
            }

            Print("  -", ConsoleColor.DarkGray)
            Print(TypeCodeText(typeCode), ConsoleColor.Cyan)
            Print(fieldName, ConsoleColor.White)

            If schemaIndex >= 0 Then
                Print("->", ConsoleColor.DarkGray)
                Print("schema#" & schemaIndex.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
            End If

            Console.WriteLine()

        Next

        s.Fields = fields
        Console.WriteLine()
        Return s

    End Function

    Private Function ReadSmatObjectFieldCountFromFirst(first As Byte) As Integer

        Select Case first
            Case 0 To SMAT_OBJ_LITERAL_MAX_FIELDS : Return CInt(first)
            Case SMAT_OBJ_LUINT_TAG : Return r.CheckedCount(r.ReadLUINT(), 2, "Object fieldCount")
            Case SMAT_NULL_TAG : Throw New Exception("SMAT NULL cannot be used as an object schema header.")
            Case Else : Throw New Exception($"Invalid SMAT for object schema: 0x{first:X2}")
        End Select

    End Function

    Private Function ReadFieldName() As String

        Dim literal As String = Nothing
        Dim ptrIndex As Integer = -1
        Dim isNull As Boolean = False

        r.ReadDSTRChunk(literal, ptrIndex, isNull)

        If isNull Then
            Throw New Exception("Field name cannot be null.")
        End If

        If ptrIndex >= 0 Then
            If _strings Is Nothing OrElse ptrIndex < 0 OrElse ptrIndex >= _strings.Length Then
                Throw New Exception("Invalid field-name string pointer index: " & ptrIndex)
            End If
            Return _strings(ptrIndex)
        End If

        Return literal

    End Function

    Private Sub ReadData()

        Console.WriteLine(":: Data ::")

        Dim first As Byte = r.PeekByte()

        If first >= SOBJ_SCHEMA_PTR_1B_TAG AndAlso first <= SOBJ_SCHEMA_PTR_3B_TAG Then

            Dim rootSchemaIndex As Integer = r.ReadSchemaPointerIndex()

            Print("rootSchema#", ConsoleColor.DarkGray)
            Print(rootSchemaIndex, ConsoleColor.Cyan)
            Console.WriteLine()

            If rootSchemaIndex < 0 OrElse rootSchemaIndex >= _schemas.Length Then
                Throw New Exception("Invalid root schema index: " & rootSchemaIndex)
            End If

            Dim root As SchemaDef = _schemas(rootSchemaIndex)
            If root Is Nothing Then
                Throw New Exception("Root schema entry is NULL at index: " & rootSchemaIndex)
            End If

            Select Case root.Kind

                Case JsonSchema.SchemaKind.Map
                    Print("ROOT MAP", ConsoleColor.Yellow)
                    Console.WriteLine()
                    ReadMapBody(root, 1)

                Case JsonSchema.SchemaKind.Array
                    Print("ROOT ARRAY", ConsoleColor.Yellow)
                    Console.WriteLine()
                    ReadArray(root.ArrayElemType, root.ArrayElemSchemaIndex, 1)

                Case Else
                    Print("ROOT OBJECT", ConsoleColor.Yellow)
                    Console.WriteLine()
                    ReadObjectSlot(root, 1)

            End Select

            Console.WriteLine()
            Return

        End If

        If first = SOBJ_NULL_TAG Then
            r.ReadByte()
            Print("ROOT NULL", ConsoleColor.Green)
            Console.WriteLine()
            Console.WriteLine()
            Return
        End If

        Dim rootTypeCode As JsonFieldType = CType(r.ReadByte(), JsonFieldType)
        Dim rootBaseType As JsonFieldType = BaseType(rootTypeCode)

        If rootBaseType < JsonFieldType.Integer OrElse rootBaseType > JsonFieldType.Bytes Then
            Throw New Exception("Invalid root type tag: 0x" & CByte(rootTypeCode).ToString("X2"))
        End If

        Print("ROOT " & TypeCodeText(rootTypeCode), ConsoleColor.Yellow)
        Console.WriteLine()

        If IsArray(rootTypeCode) Then
            ReadArray(rootBaseType, -1, 1)
        Else
            ReadScalar(rootBaseType, -1, 1)
        End If

        Console.WriteLine()

    End Sub

#End Region

#Region "Data walkers"

    Private Sub ReadObjectSlot(expected As SchemaDef, indentCount As Integer)

        Indent(indentCount)

        Dim marker As Byte = r.ReadByte()
        Print("SOBJ=" & marker.ToString("X2"), ConsoleColor.Cyan)

        Select Case marker

            Case SOBJ_NULL_TAG
                Print("NULL", ConsoleColor.Green)
                Console.WriteLine()
                Return

            Case SOBJ_PRESENT_EXPECTED_SCHEMA
                Print("expected", ConsoleColor.DarkGray)
                Console.WriteLine()
                ReadSchemaBody(expected, indentCount + 1)
                Return

            Case SOBJ_SCHEMA_PTR_1B_TAG To SOBJ_SCHEMA_PTR_3B_TAG

                Dim idx As Integer = ReadSchemaPointerIndexFromFirst(marker)

                Print("override->", ConsoleColor.DarkGray)
                Print("schema#" & idx.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
                Console.WriteLine()

                If idx < 0 OrElse idx >= _schemas.Length Then
                    Throw New Exception("Invalid schema override index: " & idx)
                End If

                If _schemas(idx) Is Nothing Then
                    Throw New Exception("Schema override points to NULL entry: " & idx)
                End If

                ReadSchemaBody(_schemas(idx), indentCount + 1)
                Return

            Case Else

                Dim typeCode As JsonFieldType = CType(marker, JsonFieldType)
                Dim valueBaseType As JsonFieldType = BaseType(typeCode)

                If valueBaseType = JsonFieldType.Object Then
                    If Not IsArray(typeCode) Then
                        Throw New Exception("Invalid SOBJ marker: Object scalar override is not allowed.")
                    End If

                    Print("Object[]", ConsoleColor.White)
                    Console.WriteLine()
                    ReadObjectArrayOverrideExpected(expected, indentCount + 1)
                    Return
                End If

                Print(TypeCodeText(typeCode), ConsoleColor.White)
                Console.WriteLine()

                If IsArray(typeCode) Then
                    ReadArray(valueBaseType, -1, indentCount + 1)
                Else
                    ReadScalar(valueBaseType, -1, indentCount + 1)
                End If

        End Select

    End Sub

    Private Sub ReadSchemaBody(schema As SchemaDef, indentCount As Integer)

        If schema Is Nothing Then
            Indent(indentCount)
            Print("NULL schema body", ConsoleColor.Green)
            Console.WriteLine()
            Return
        End If

        Select Case schema.Kind

            Case JsonSchema.SchemaKind.Map
                Indent(indentCount)
                Print("MapBody", ConsoleColor.Yellow)
                Console.WriteLine()
                ReadMapBody(schema, indentCount + 1)

            Case JsonSchema.SchemaKind.Array
                Indent(indentCount)
                Print("ArrayBody", ConsoleColor.Yellow)
                Console.WriteLine()
                ReadArray(schema.ArrayElemType, schema.ArrayElemSchemaIndex, indentCount + 1)

            Case Else

                If schema.Fields Is Nothing OrElse schema.Fields.Length = 0 Then
                    Indent(indentCount)
                    Print("{ } (0 fields)", ConsoleColor.DarkGray)
                    Console.WriteLine()
                    Return
                End If

                Indent(indentCount)
                Print("{ fields=" & schema.Fields.Length.ToString(CultureInfo.InvariantCulture) & " }", ConsoleColor.DarkGray)
                Console.WriteLine()

                For i As Integer = 0 To schema.Fields.Length - 1
                    Dim f As FieldDef = schema.Fields(i)

                    Indent(indentCount)
                    Print("-", ConsoleColor.DarkGray)
                    Print(f.Name, ConsoleColor.White)
                    Print(":" & TypeCodeText(f.TypeCode), ConsoleColor.Cyan)
                    Console.WriteLine()

                    ReadValue(f.TypeCode, f.SchemaIndex, indentCount + 1)
                Next

        End Select

    End Sub

    Private Sub ReadMapBody(schema As SchemaDef, indentCount As Integer)

        Dim count As Integer = r.CheckedCount(r.ReadLUINT(), 2, "Map count")

        Indent(indentCount)
        Print("count=" & count.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
        Print("valueType=" & TypeCodeText(schema.MapValueType), ConsoleColor.DarkGray)
        Console.WriteLine()

        For i As Integer = 0 To count - 1

            Indent(indentCount)
            Print("[" & i.ToString(CultureInfo.InvariantCulture) & "]", ConsoleColor.DarkGray)
            Print("key=", ConsoleColor.DarkGray)

            Dim key As String = ReadDstrValue()

            If key Is Nothing Then
                Print("NULL", ConsoleColor.Green)
            Else
                Print(key, ConsoleColor.Yellow)
            End If

            Console.WriteLine()
            ReadValue(schema.MapValueType, schema.MapValueSchemaIndex, indentCount + 1)

        Next

    End Sub

    Private Sub ReadValue(typeCode As JsonFieldType, schemaIndex As Integer, indentCount As Integer)
        If IsArray(typeCode) Then
            ReadArray(BaseType(typeCode), schemaIndex, indentCount)
        Else
            ReadScalar(BaseType(typeCode), schemaIndex, indentCount)
        End If
    End Sub

    Private Sub ReadObjectArrayOverrideExpected(expected As SchemaDef, indentCount As Integer)

        Dim isNull As Boolean
        Dim countValue As Long = r.ReadLUINTOrNull(isNull)

        If isNull Then
            Indent(indentCount)
            Print("count=NULL", ConsoleColor.Green)
            Console.WriteLine()
            Return
        End If

        Dim count As Integer = r.CheckedCount(countValue, 1, "Object[] count")

        Indent(indentCount)
        Print("count=" & count.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
        Console.WriteLine()

        For i As Integer = 0 To count - 1
            Indent(indentCount)
            Print("[" & i.ToString(CultureInfo.InvariantCulture) & "]", ConsoleColor.DarkGray)
            Console.WriteLine()
            ReadObjectSlot(expected, indentCount + 1)
        Next

    End Sub

    Private Sub ReadArray(baseType As JsonFieldType, schemaIndex As Integer, indentCount As Integer)

        Dim isNull As Boolean
        Dim countValue As Long = r.ReadLUINTOrNull(isNull)

        Indent(indentCount)
        Print(baseType.ToString() & "[]", ConsoleColor.Cyan)

        If isNull Then
            Print("count=NULL", ConsoleColor.Green)
            Console.WriteLine()
            Return
        End If

        Dim count As Integer

        Select Case baseType
            Case JsonFieldType.Integer : count = r.CheckedCount(countValue, 1, "Integer[] count")
            Case JsonFieldType.Float4Bytes : count = r.CheckedCount(countValue, 4, "Float4[] count")
            Case JsonFieldType.Float8Bytes : count = r.CheckedCount(countValue, 8, "Float8[] count")
            Case JsonFieldType.Boolean : count = r.CheckedCount(countValue, 1, "Boolean[] count")
            Case JsonFieldType.Date : count = r.CheckedCount(countValue, 1, "Date[] count")
            Case JsonFieldType.String : count = r.CheckedCount(countValue, 1, "String[] count")
            Case JsonFieldType.Bytes : count = r.CheckedCount(countValue, 1, "Bytes[] count")
            Case JsonFieldType.Object : count = r.CheckedCount(countValue, 1, "Object[] count")
            Case Else : Throw New NotSupportedException("Unsupported array base type: " & baseType.ToString())
        End Select

        Print("count=" & count.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)
        Console.WriteLine()

        For i As Integer = 0 To count - 1

            Indent(indentCount)
            Print("[" & i.ToString(CultureInfo.InvariantCulture) & "]", ConsoleColor.DarkGray)

            If baseType = JsonFieldType.Object Then

                Console.WriteLine()

                If schemaIndex < 0 OrElse schemaIndex >= _schemas.Length Then
                    Throw New Exception("Invalid expected schema index for Object[]: " & schemaIndex)
                End If

                If _schemas(schemaIndex) Is Nothing Then
                    Throw New Exception("Object[] expected schema points to NULL entry: " & schemaIndex)
                End If

                ReadObjectSlot(_schemas(schemaIndex), indentCount + 1)

            Else
                ReadScalarInline(baseType)
                Console.WriteLine()
            End If

        Next

    End Sub

    Private Sub ReadScalar(baseType As JsonFieldType, schemaIndex As Integer, indentCount As Integer)

        Indent(indentCount)
        Print(baseType.ToString(), ConsoleColor.Cyan)

        If baseType = JsonFieldType.Object Then

            Console.WriteLine()

            If schemaIndex < 0 OrElse schemaIndex >= _schemas.Length Then
                Throw New Exception("Invalid expected schema index for Object: " & schemaIndex)
            End If

            If _schemas(schemaIndex) Is Nothing Then
                Throw New Exception("Object expected schema points to NULL entry: " & schemaIndex)
            End If

            ReadObjectSlot(_schemas(schemaIndex), indentCount + 1)
            Return

        End If

        ReadScalarInline(baseType)
        Console.WriteLine()

    End Sub

    Private Sub ReadScalarInline(baseType As JsonFieldType)

        Select Case baseType

            Case JsonFieldType.Integer
                Dim n As Boolean
                Dim v As Long = r.ReadLINTOrNull(n)
                If n Then Print("NULL", ConsoleColor.Green) Else Print(v, ConsoleColor.White)

            Case JsonFieldType.Boolean
                Dim n As Boolean
                Dim v As Boolean = r.ReadBoolOrNull(n)
                If n Then
                    Print("NULL", ConsoleColor.Green)
                Else
                    Print(If(v, "true", "false"), ConsoleColor.White)
                End If

            Case JsonFieldType.Float4Bytes
                Dim n As Boolean
                Dim v As Single = r.ReadSingleOrNull(n)
                If n Then
                    Print("NULL", ConsoleColor.Green)
                Else
                    Print(v.ToString(CultureInfo.InvariantCulture), ConsoleColor.White)
                End If

            Case JsonFieldType.Float8Bytes
                Dim n As Boolean
                Dim v As Double = r.ReadDoubleOrNull(n)
                If n Then
                    Print("NULL", ConsoleColor.Green)
                Else
                    Print(v.ToString(CultureInfo.InvariantCulture), ConsoleColor.White)
                End If

            Case JsonFieldType.Date

                Dim literalUtc As DateTime
                Dim ptrIndex As Integer
                Dim isNull As Boolean

                r.ReadDDate(literalUtc, ptrIndex, isNull)

                If isNull Then
                    Print("NULL", ConsoleColor.Green)
                ElseIf ptrIndex >= 0 Then
                    If _dates Is Nothing OrElse ptrIndex < 0 OrElse ptrIndex >= _dates.Length Then
                        Print("<BAD_DATE_PTR:" & ptrIndex.ToString(CultureInfo.InvariantCulture) & ">", ConsoleColor.Red)
                    Else
                        Print(_dates(ptrIndex).ToString("o"), ConsoleColor.White)
                        Print("(ptr#" & ptrIndex.ToString(CultureInfo.InvariantCulture) & ")", ConsoleColor.DarkGray)
                    End If
                Else
                    Print(literalUtc.ToString("o"), ConsoleColor.White)
                End If

            Case JsonFieldType.String
                Dim s As String = ReadDstrValue()
                If s Is Nothing Then Print("NULL", ConsoleColor.Green) Else Print(s, ConsoleColor.Yellow)

            Case JsonFieldType.Bytes
                Dim n As Boolean
                Dim b() As Byte = r.ReadBarrOrNull(n)

                If n Then
                    Print("NULL", ConsoleColor.Green)
                Else
                    Print("len=" & b.Length.ToString(CultureInfo.InvariantCulture), ConsoleColor.Cyan)

                    Dim p As Integer = Math.Min(16, b.Length)
                    If p > 0 Then
                        Print("hex:", ConsoleColor.DarkGray)
                        For i As Integer = 0 To p - 1
                            Print(b(i).ToString("X2"), ConsoleColor.DarkGray)
                        Next
                        If b.Length > p Then
                            Print("... (+" & (b.Length - p).ToString(CultureInfo.InvariantCulture) & ")", ConsoleColor.DarkGray)
                        End If
                    End If
                End If

            Case Else
                Throw New NotSupportedException("Unsupported scalar base type: " & baseType.ToString())

        End Select

    End Sub

#End Region

#Region "Viewer helpers"

    Private Function ReadSchemaPointerIndexFromFirst(firstTag As Byte) As Integer

        Select Case firstTag
            Case SOBJ_SCHEMA_PTR_1B_TAG : Return CInt(r.ReadByte())
            Case SOBJ_SCHEMA_PTR_2B_TAG : Return (CInt(r.ReadByte()) << 8) Or CInt(r.ReadByte())
            Case SOBJ_SCHEMA_PTR_3B_TAG : Return (CInt(r.ReadByte()) << 16) Or (CInt(r.ReadByte()) << 8) Or CInt(r.ReadByte())
            Case Else : Throw New Exception("Invalid schema pointer tag: 0x" & firstTag.ToString("X2"))
        End Select

    End Function

    Private Function ReadDstrValue() As String

        Dim literal As String = Nothing
        Dim ptrIndex As Integer = -1
        Dim isNull As Boolean = False

        r.ReadDSTRChunk(literal, ptrIndex, isNull)

        If isNull Then
            Return Nothing
        End If

        If ptrIndex >= 0 Then
            If _strings Is Nothing OrElse ptrIndex < 0 OrElse ptrIndex >= _strings.Length Then
                Return "<BAD_PTR:" & ptrIndex.ToString(CultureInfo.InvariantCulture) & ">"
            End If
            Return _strings(ptrIndex)
        End If

        Return literal

    End Function

    Private Shared Sub Indent(level As Integer)
        If level <= 0 Then Return
        Console.Write(New String(" "c, level * 2))
    End Sub

    Private Shared Function IsArray(tc As JsonFieldType) As Boolean
        Return JsonField.FieldTypeIsArray(tc)
    End Function

    Private Shared Function BaseType(tc As JsonFieldType) As JsonFieldType
        Return JsonField.FieldTypeWithoutArrayFlag(tc)
    End Function

    Private Shared Function TypeCodeText(tc As JsonFieldType) As String
        Return BaseType(tc).ToString() & If(IsArray(tc), "[]", "")
    End Function

    Private Shared Function FormatHeaderValue(value As Object) As String

        If value Is Nothing Then
            Return "NULL"
        End If

        If TypeOf value Is String Then
            Return DirectCast(value, String)
        End If

        If TypeOf value Is Byte() Then
            Dim b() As Byte = DirectCast(value, Byte())
            Return "bytes[" & b.Length.ToString(CultureInfo.InvariantCulture) & "]"
        End If

        If TypeOf value Is DateTime Then
            Return DirectCast(value, DateTime).ToString("o")
        End If

        Dim arr As Array = TryCast(value, Array)
        If arr IsNot Nothing Then
            Dim parts As New List(Of String)()
            Dim max As Integer = Math.Min(arr.Length, 8)

            For i As Integer = 0 To max - 1
                Dim item As Object = arr.GetValue(i)

                If item Is Nothing Then
                    parts.Add("NULL")
                ElseIf TypeOf item Is DateTime Then
                    parts.Add(DirectCast(item, DateTime).ToString("o"))
                ElseIf TypeOf item Is Byte() Then
                    parts.Add("bytes[" & DirectCast(item, Byte()).Length.ToString(CultureInfo.InvariantCulture) & "]")
                Else
                    parts.Add(Convert.ToString(item, CultureInfo.InvariantCulture))
                End If
            Next

            If arr.Length > max Then
                parts.Add("...")
            End If

            Return "[" & String.Join(", ", parts) & "]"
        End If

        Return Convert.ToString(value, CultureInfo.InvariantCulture)

    End Function

#End Region

#Region "Hex coloring"

    Private Sub PrintHexLegend()

        Console.Write("HEX colors: ")
        Print("MAGIC/V/ZMSK", ConsoleColor.Gray)
        Print("HEADER", ConsoleColor.Cyan)
        Print("FILES", ConsoleColor.Blue)
        Print("STRINGS", ConsoleColor.Red)
        Print("DATES", ConsoleColor.DarkGreen)
        Print("SCHEMAS", ConsoleColor.Magenta)
        Print("DATA", ConsoleColor.Yellow)
        Console.WriteLine()
        Console.WriteLine()

    End Sub

    Private Function GetHexForeColor(offset As Integer) As ConsoleColor

        If _hexSpans IsNot Nothing Then
            For i As Integer = 0 To _hexSpans.Length - 1
                Dim s As HexSpan = _hexSpans(i)
                If offset >= s.Start AndAlso offset < s.End Then
                    Return s.Fore
                End If
            Next
        End If

        Return ConsoleColor.Gray

    End Function

    Private Sub BuildHexSpans()

        Try

            Dim spans As New List(Of HexSpan)()
            Dim rr As New Decoding.Reader(_data)

            Dim fixedStart As Integer = rr.Position()
            rr.ReadByte()
            rr.ReadByte()
            rr.ReadByte()
            rr.ReadByte()
            rr.ReadByte() ' version
            Dim zmsk As Byte = rr.ReadByte()

            spans.Add(New HexSpan With {
                .Start = fixedStart,
                .End = rr.Position(),
                .Fore = ConsoleColor.Gray,
                .Label = "MAGIC/V/ZMSK"
            })

            If (zmsk And ZMSK_HAS_NEXT) <> 0 Then
                Throw New Exception("Unsupported ZMSK chain in hex scan.")
            End If

            Dim unsupported As Integer =
                (CInt(zmsk) And Not CInt(ZMSK_V1_DEFINED_ZONES_MASK) And &HFF)

            If unsupported <> 0 Then
                Throw New Exception("Unsupported ZMSK bits in hex scan: 0x" & unsupported.ToString("X2"))
            End If

            If (zmsk And ZMSK_HEADERS) <> 0 Then
                Dim p0 As Integer = rr.Position()
                rr.ReadHeader()
                spans.Add(New HexSpan With {
                    .Start = p0,
                    .End = rr.Position(),
                    .Fore = ConsoleColor.Cyan,
                    .Label = "HEADER"
                })
            End If

            If (zmsk And ZMSK_FILES) <> 0 Then
                Dim p0 As Integer = rr.Position()
                ScanFilesZone(rr)
                spans.Add(New HexSpan With {
                    .Start = p0,
                    .End = rr.Position(),
                    .Fore = ConsoleColor.Blue,
                    .Label = "FILES"
                })
            End If

            If (zmsk And ZMSK_STRING_TABLE) <> 0 Then
                Dim p0 As Integer = rr.Position()
                ScanStringTable(rr)
                spans.Add(New HexSpan With {
                    .Start = p0,
                    .End = rr.Position(),
                    .Fore = ConsoleColor.Red,
                    .Label = "STRINGS"
                })
            End If

            If (zmsk And ZMSK_DATE_TABLE) <> 0 Then
                Dim p0 As Integer = rr.Position()
                ScanDateTable(rr)
                spans.Add(New HexSpan With {
                    .Start = p0,
                    .End = rr.Position(),
                    .Fore = ConsoleColor.DarkGreen,
                    .Label = "DATES"
                })
            End If

            If (zmsk And ZMSK_SCHEMA_TABLE) <> 0 Then
                Dim p0 As Integer = rr.Position()
                ScanSchemaTable(rr)
                spans.Add(New HexSpan With {
                    .Start = p0,
                    .End = rr.Position(),
                    .Fore = ConsoleColor.Magenta,
                    .Label = "SCHEMAS"
                })
            End If

            If (zmsk And ZMSK_DATA) <> 0 Then
                spans.Add(New HexSpan With {
                    .Start = rr.Position(),
                    .End = _data.Length,
                    .Fore = ConsoleColor.Yellow,
                    .Label = "DATA"
                })
            End If

            _hexSpans = spans.ToArray()

        Catch
            _hexSpans = Array.Empty(Of HexSpan)()
        End Try

    End Sub

    Private Shared Sub ScanFilesZone(rr As Decoding.Reader)

        Dim declaredBodyLength As Integer =
        rr.CheckedLength(rr.ReadLUINT(), "Files body length")

        If declaredBodyLength < 1 Then
            Throw New Exception("FILES body length cannot be 0. It must include the serialized fileCount.")
        End If

        Dim startPos As Integer = rr.Position()

        Dim count As Integer =
        rr.CheckedCount(rr.ReadLUINT(), 1, "Files count")

        If count = 0 Then
            Throw New Exception("FILES zone must be omitted when fileCount = 0.")
        End If

        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)

        For i As Integer = 0 To count - 1

            Dim isNull As Boolean
            Dim nameLen As Long = rr.ReadLUINTOrNull(isNull)
            If isNull Then Throw New Exception("FILES zone file name cannot be null.")

            Dim checkedNameLen As Integer = rr.CheckedLength(nameLen, "File name length")
            Dim fileName As String = rr.ReadUtf8Literal(checkedNameLen)

            If String.IsNullOrWhiteSpace(fileName) Then
                Throw New Exception("FILES zone cannot contain null, empty or whitespace file names.")
            End If

            If Not seen.Add(fileName) Then
                Throw New Exception("Duplicate file name in FILES zone: " & fileName)
            End If

            Dim payload() As Byte = rr.ReadBarrOrNull(isNull)
            If isNull Then Throw New Exception("FILES zone file payload cannot be null.")

        Next

        Dim actualBodyLength As Integer = rr.Position() - startPos

        If actualBodyLength <> declaredBodyLength Then
            Throw New Exception(
            $"FILES body length mismatch in scan. Declared={declaredBodyLength}, actual={actualBodyLength}.")
        End If

    End Sub

    Private Shared Sub ScanStringTable(rr As Decoding.Reader)

        Dim count As Integer = rr.CheckedCount(rr.ReadLUINT(), 1, "String table count")

        For i As Integer = 0 To count - 1
            Dim isNull As Boolean
            Dim len As Long = rr.ReadLUINTOrNull(isNull)
            If isNull Then Throw New Exception("String table cannot contain NULL entries.")
            rr.ReadBytesLiteral(rr.CheckedLength(len, "String table entry length"))
        Next

    End Sub

    Private Shared Sub ScanDateTable(rr As Decoding.Reader)

        Dim count As Integer = rr.CheckedCount(rr.ReadLUINT(), 8, "Date table count")

        For i As Integer = 0 To count - 1
            Dim isNull As Boolean
            rr.ReadLDate(isNull)
            If isNull Then Throw New Exception("Date table cannot contain NULL entries.")
        Next

    End Sub

    Private Shared Sub ScanSchemaTable(rr As Decoding.Reader)

        Dim schemaCount As Integer = rr.CheckedCount(rr.ReadLUINT(), 1, "Schema count")

        For i As Integer = 0 To schemaCount - 1

            Dim smat As Byte = rr.ReadByte()

            Select Case smat

                Case SMAT_NULL_TAG

                Case SMAT_MAP_BASE_TAG To SMAT_ARR_MAX_TAG

                    Dim typeId As Integer

                    Select Case smat
                        Case SMAT_MAP_BASE_TAG To SMAT_MAP_MAX_TAG
                            typeId = (CInt(smat) - CInt(SMAT_MAP_BASE_TAG)) + 1

                        Case SMAT_MAP_ARR_BASE_TAG To SMAT_MAP_ARR_MAX_TAG
                            typeId = (CInt(smat) - CInt(SMAT_MAP_ARR_BASE_TAG)) + 1

                        Case Else
                            typeId = (CInt(smat) - CInt(SMAT_ARR_BASE_TAG)) + 1
                    End Select

                    If typeId < SMAT_TYPE_MIN_ID OrElse typeId > SMAT_TYPE_MAX_ID Then
                        Throw New Exception("Invalid SMAT typeId in scan: " & typeId)
                    End If

                    If CType(typeId, JsonFieldType) = JsonFieldType.Object Then
                        rr.ReadSchemaPointerIndex()
                    End If

                Case Else

                    Dim fieldCount As Integer

                    Select Case smat
                        Case 0 To SMAT_OBJ_LITERAL_MAX_FIELDS : fieldCount = CInt(smat)
                        Case SMAT_OBJ_LUINT_TAG : fieldCount = rr.CheckedCount(rr.ReadLUINT(), 2, "Object fieldCount")
                        Case Else : Throw New Exception("Invalid SMAT for object schema in scan: 0x" & smat.ToString("X2"))
                    End Select

                    For f As Integer = 0 To fieldCount - 1

                        Dim typeCode As JsonFieldType = CType(rr.ReadByte(), JsonFieldType)

                        Dim literal As String = Nothing
                        Dim ptrIndex As Integer = -1
                        Dim isNull As Boolean = False
                        rr.ReadDSTRChunk(literal, ptrIndex, isNull)
                        If isNull Then Throw New Exception("Field name cannot be null.")

                        If JsonField.FieldTypeWithoutArrayFlag(typeCode) = JsonFieldType.Object Then
                            rr.ReadSchemaPointerIndex()
                        End If

                    Next

            End Select

        Next

    End Sub

#End Region

#Region "Compression"

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function IsGZip(data As Byte()) As Boolean
        Return data IsNot Nothing AndAlso data.Length >= 2 AndAlso data(0) = &H1F AndAlso data(1) = &H8B
    End Function

    Private Shared Function DecompressGZip(data As Byte()) As Byte()

        If data Is Nothing Then
            Return Nothing
        End If

        Using input As New MemoryStream(data)
            Using gz As New IO.Compression.GZipStream(input, IO.Compression.CompressionMode.Decompress)
                Using output As New MemoryStream()
                    gz.CopyTo(output)
                    Return output.ToArray()
                End Using
            End Using
        End Using

    End Function

#End Region

End Class