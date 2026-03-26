Imports System.IO
Imports System.Runtime.CompilerServices
Imports Bytery.JSON
Imports Bytery.Linq

Namespace Decoding

    ''' <summary>
    ''' Decodes a complete Bytery container into the Bytery DOM representation.
    ''' </summary>
    ''' <remarks>
    ''' High-level pipeline:
    '''   1. Optionally normalize the input buffer (raw Bytery or GZIP-wrapped Bytery).
    '''   2. Validate container magic and version.
    '''   3. Read the ZMSK presence byte.
    '''   4. Read only the zones declared by ZMSK, in canonical order.
    '''   5. Read the FILES zone when present.
    '''   6. Decode the root DATA payload when present.
    '''
    ''' Canonical zone order:
    '''   [header][files][string table][date table][schema table][data]
    '''
    ''' The decoder supports:
    '''   - raw Bytery payloads
    '''   - GZIP-wrapped Bytery payloads
    '''   - root object / map / array via schema pointer
    '''   - root primitive / primitive array via direct type tag
    '''   - root null
    '''   - optional FILES zone returned through a ByRef output parameter
    '''
    ''' Notes:
    '''   - HEADER and FILES use their current v1 zone-body layouts.
    '''   - Zone absence is now decided at the container level by ZMSK.
    ''' </remarks>
    Friend NotInheritable Class Decoder

#Region "Schema Definitions"

        ''' <summary>
        ''' Session-local schema model reconstructed from the SchemaTable.
        ''' </summary>
        ''' <remarks>
        ''' This is not the global JsonSchema type.
        ''' It is the decoded, runtime-ready representation used while reading the data section.
        ''' </remarks>
        Private NotInheritable Class SchemaDef

            ''' <summary>
            ''' Gets or sets the schema kind.
            ''' </summary>
            Public Kind As JsonSchema.SchemaKind

            ''' <summary>
            ''' Object-schema fields.
            ''' Used only when <see cref="Kind"/> is <c>Object</c>.
            ''' </summary>
            Public Fields() As FieldDef

            ''' <summary>
            ''' Map value type.
            ''' Used only when <see cref="Kind"/> is <c>Map</c>.
            ''' </summary>
            Public MapValueType As JsonFieldType = JsonFieldType.Unknown

            ''' <summary>
            ''' Referenced schema index for map values whose base type is Object.
            ''' </summary>
            Public MapValueSchemaIndex As Integer = -1

            ''' <summary>
            ''' Array element type.
            ''' Used only when <see cref="Kind"/> is <c>Array</c>.
            ''' </summary>
            Public ArrayElemType As JsonFieldType = JsonFieldType.Unknown

            ''' <summary>
            ''' Referenced schema index for array elements whose base type is Object.
            ''' </summary>
            Public ArrayElemSchemaIndex As Integer = -1

        End Class

        ''' <summary>
        ''' Session-local field descriptor reconstructed from an object schema entry.
        ''' </summary>
        Private Structure FieldDef
            Public Name As String
            Public NamePtrIndex As Integer
            Public TypeCode As JsonFieldType
            Public SchemaIndex As Integer
        End Structure

#End Region

        Private ReadOnly R As Reader
        Private _schemas() As SchemaDef
        Private _strings() As String
        Private _dates() As DateTime
        Private _fileVersion As Byte
        Private _zoneMask As Byte

        ''' <summary>
        ''' Initializes a decoder over a full Bytery container buffer.
        ''' </summary>
        ''' <param name="source">The encoded Bytery payload.</param>
        Private Sub New(source As Byte())
            R = New Reader(source)
        End Sub

#Region "Root Decoder"

        ''' <summary>
        ''' Returns whether the supplied buffer starts with the GZIP magic header.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function IsGZip(data As Byte()) As Boolean
            Return data IsNot Nothing AndAlso
                   data.Length >= 2 AndAlso
                   data(0) = &H1F AndAlso
                   data(1) = &H8B
        End Function

        ''' <summary>
        ''' Returns whether the supplied buffer starts with the raw Bytery container magic.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function IsBytery(data As Byte()) As Boolean
            Return data IsNot Nothing AndAlso
                   data.Length >= 5 AndAlso
                   data(0) = FILE_MAGIC_B0 AndAlso
                   data(1) = FILE_MAGIC_B1 AndAlso
                   data(2) = FILE_MAGIC_B2 AndAlso
                   data(3) = FILE_MAGIC_B3
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function IsJson(data As Byte()) As Boolean

            If data Is Nothing OrElse data.Length = 0 Then Return False

            For i As Integer = 0 To data.Length - 1

                Dim b As Byte = data(i)

                Select Case b
                    Case 9, 10, 13, 32
                        Continue For

                    Case AscW("{"c), AscW("["c), AscW(""""c), AscW("-"c), AscW("t"c), AscW("f"c), AscW("n"c)
                        Return True

                    Case AscW("0"c) To AscW("9"c)
                        Return True

                    Case Else
                        Return False
                End Select

            Next

            Return False

        End Function

        ''' <summary>
        ''' Decompresses a GZIP-wrapped payload into its raw inner bytes.
        ''' </summary>
        ''' <param name="data">The outer GZIP payload.</param>
        ''' <returns>The decompressed raw bytes.</returns>
        Private Shared Function DecompressGZip(data As Byte()) As Byte()

            If data Is Nothing Then
                Return Nothing
            End If

            Using input As New MemoryStream(data)
                Using gz As New System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress)
                    Using output As New MemoryStream()
                        gz.CopyTo(output)
                        Return output.ToArray()
                    End Using
                End Using
            End Using

        End Function

        Public Shared Function Decode(source As Byte(),
                      Optional ignoreHeader As Boolean = False,
                      Optional ignoreFiles As Boolean = False,
                      Optional ByRef headerOut As List(Of HeaderEntry) = Nothing,
                      Optional ByRef filesOut As List(Of KeyValuePair(Of String, Byte())) = Nothing) As BToken

            If source Is Nothing OrElse source.Length = 0 Then
                headerOut = Nothing
                filesOut = Nothing
                Return BNull.Instance(JsonFieldType.Unknown)
            End If

            If IsGZip(source) Then source = DecompressGZip(source)
            If IsJson(source) Then
                headerOut = Nothing
                filesOut = Nothing
                Return BToken.ParseJSON(System.Text.Encoding.UTF8.GetString(source))
            End If

            If Not IsBytery(source) Then
                Throw New Exception("Source bytes arent Bytery (failed magic bytes)")
            End If

            Dim d As New Decoder(source)

            d.ReadMagic()
            d.ReadZoneMask()

            ' ---------------------------------------------------------------------
            ' HEADER
            ' ---------------------------------------------------------------------
            If d.HasZone(ZMSK_HEADERS) Then
                If ignoreHeader Then
                    d.R.SkipHeader()
                    headerOut = Nothing
                Else
                    headerOut = d.R.ReadHeader()
                End If
            Else
                headerOut = Nothing
            End If

            ' ---------------------------------------------------------------------
            ' FILES
            ' ---------------------------------------------------------------------
            If d.HasZone(ZMSK_FILES) Then
                If ignoreFiles Then
                    d.R.SkipFiles()
                    filesOut = Nothing
                Else
                    d.ReadFilesZone(filesOut)
                End If
            Else
                filesOut = Nothing
            End If

            ' ---------------------------------------------------------------------
            ' STRING TABLE
            ' ---------------------------------------------------------------------
            If d.HasZone(ZMSK_STRING_TABLE) Then
                d.ReadStringTable()
            Else
                d._strings = Array.Empty(Of String)()
            End If

            ' ---------------------------------------------------------------------
            ' DATE TABLE
            ' ---------------------------------------------------------------------
            If d.HasZone(ZMSK_DATE_TABLE) Then
                d.ReadDateTable()
            Else
                d._dates = Array.Empty(Of DateTime)()
            End If

            ' ---------------------------------------------------------------------
            ' SCHEMA TABLE
            ' ---------------------------------------------------------------------
            If d.HasZone(ZMSK_SCHEMA_TABLE) Then
                d.ReadSchemas()
                d.ResolveSchemaNames()
            Else
                d._schemas = Array.Empty(Of SchemaDef)()
            End If

            ' ---------------------------------------------------------------------
            ' DATA
            ' ---------------------------------------------------------------------
            Dim result As BToken = BNull.Instance(JsonFieldType.Unknown)

            If d.HasZone(ZMSK_DATA) Then

                Dim first As Byte = d.R.PeekByte()

                If first >= SOBJ_SCHEMA_PTR_1B_TAG AndAlso first <= SOBJ_SCHEMA_PTR_3B_TAG Then

                    Dim rootSchemaIndex As Integer = d.R.ReadSchemaPointerIndex()

                    If rootSchemaIndex < 0 OrElse rootSchemaIndex >= d._schemas.Length Then
                        Throw New Exception($"Invalid root schema index: {rootSchemaIndex}")
                    End If

                    Dim rootSchema = d._schemas(rootSchemaIndex)
                    If rootSchema Is Nothing Then
                        Throw New Exception($"Root schema entry is NULL at index: {rootSchemaIndex}")
                    End If

                    Select Case rootSchema.Kind
                        Case JsonSchema.SchemaKind.Map
                            result = d.ReadMapBody(rootSchema)

                        Case JsonSchema.SchemaKind.Array
                            result = d.ReadArray(rootSchema.ArrayElemType, rootSchema.ArrayElemSchemaIndex)

                        Case Else
                            result = d.ReadObjectValue(rootSchema)
                    End Select

                ElseIf first = SOBJ_NULL_TAG Then

                    d.R.ReadByte()
                    result = BNull.Instance(JsonFieldType.Object)

                Else

                    Dim rootTypeCode As JsonFieldType = CType(d.R.ReadByte(), JsonFieldType)
                    Dim rootBaseType As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(rootTypeCode)

                    If rootBaseType < JsonFieldType.Integer OrElse rootBaseType > JsonFieldType.Bytes Then
                        Throw New Exception($"Invalid root type tag: 0x{CByte(rootTypeCode):X2}")
                    End If

                    If IsArray(rootTypeCode) Then
                        result = d.ReadArray(rootBaseType, -1)
                    Else
                        result = d.ReadScalar(rootBaseType, -1)
                    End If

                End If

            End If

            ' ---------------------------------------------------------------------
            ' Trailing validation
            ' ---------------------------------------------------------------------
            If d.R.Remaining() <> 0 Then
                Throw New Exception($"Unexpected trailing bytes after container decode: remaining={d.R.Remaining()}.")
            End If

            Return result

        End Function

#End Region

#Region "[magic]"

        ''' <summary>
        ''' Reads and validates the file magic and container version.
        ''' </summary>
        ''' <remarks>
        ''' Expected layout:
        '''   [B][Y][T][1][version]
        ''' </remarks>
        Private Sub ReadMagic()

            Dim b0 As Byte = R.ReadByte()
            Dim b1 As Byte = R.ReadByte()
            Dim b2 As Byte = R.ReadByte()
            Dim b3 As Byte = R.ReadByte()

            If b0 <> FILE_MAGIC_B0 OrElse b1 <> FILE_MAGIC_B1 OrElse b2 <> FILE_MAGIC_B2 OrElse b3 <> FILE_MAGIC_B3 Then
                Throw New Exception($"Invalid file magic. Expected BYT1, got 0x{b0:X2} 0x{b1:X2} 0x{b2:X2} 0x{b3:X2}.")
            End If

            _fileVersion = R.ReadByte()

            If _fileVersion <> FILE_VERSION_V1 Then
                Throw New Exception("Unsupported file version: " & _fileVersion)
            End If

        End Sub

#End Region

#Region "[zmsk]"

        ''' <summary>
        ''' Reads and validates the first ZMSK byte.
        ''' </summary>
        ''' <remarks>
        ''' v1 currently supports a single ZMSK byte only.
        ''' The continuation bit is therefore rejected for now.
        ''' </remarks>
        Private Sub ReadZoneMask()

            _zoneMask = R.ReadByte()

            If (_zoneMask And ZMSK_HAS_NEXT) <> 0 Then
                Throw New Exception("Unsupported ZMSK chain: continuation bit is set, but this decoder supports only one ZMSK byte.")
            End If

            Dim unsupported As Integer =
                (CInt(_zoneMask) And Not CInt(ZMSK_V1_DEFINED_ZONES_MASK) And &HFF)

            If unsupported <> 0 Then
                Throw New Exception($"Unsupported ZMSK bits for this decoder: 0x{unsupported:X2}")
            End If

        End Sub

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function HasZone(bit As Byte) As Boolean
            Return (_zoneMask And bit) = bit
        End Function

#End Region

#Region "[strings]"

        Private Sub ReadStringTable()

            Dim count As Integer = R.CheckedCount(R.ReadLUINT(), 1, "String table count")

            If count = 0 Then
                _strings = Array.Empty(Of String)()
                Return
            End If

            Dim table(count - 1) As String

            For i As Integer = 0 To count - 1

                Dim isNull As Boolean
                Dim len As Long = R.ReadLUINTOrNull(isNull)

                If isNull Then
                    Throw New Exception("String table cannot contain NULL entries.")
                End If

                table(i) = R.ReadUtf8Literal(R.CheckedLength(len, "String table entry length"))

            Next

            _strings = table

        End Sub

#End Region

#Region "[date]"

        Private Sub ReadDateTable()

            Dim count As Integer = R.CheckedCount(R.ReadLUINT(), 8, "Date table count")

            If count = 0 Then
                _dates = Array.Empty(Of DateTime)()
                Return
            End If

            Dim table(count - 1) As DateTime

            For i As Integer = 0 To count - 1

                Dim isNull As Boolean
                Dim dt As DateTime = R.ReadLDate(isNull)

                If isNull Then
                    Throw New Exception("Date table cannot contain NULL entries.")
                End If

                table(i) = dt

            Next

            _dates = table

        End Sub

#End Region

#Region "[schemas]"

        Private Sub ReadSchemas()

            Dim schemaCount As Integer = R.CheckedCount(R.ReadLUINT(), 1, "Schema count")

            If schemaCount = 0 Then
                _schemas = Array.Empty(Of SchemaDef)()
                Return
            End If

            Dim schemas(schemaCount - 1) As SchemaDef

            For i As Integer = 0 To schemaCount - 1

                Dim smat As Byte = R.ReadByte()

                Select Case smat

                    Case SMAT_NULL_TAG
                        schemas(i) = Nothing

                    Case SMAT_MAP_BASE_TAG To SMAT_ARR_MAX_TAG
                        schemas(i) = ReadNonObjectSchema(smat)

                    Case Else
                        schemas(i) = ReadObjectSchema(smat)

                End Select

            Next

            _schemas = schemas

        End Sub

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
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

                Case SMAT_MAP_ARR_BASE_TAG To SMAT_MAP_ARR_MAX_TAG
                    s.Kind = JsonSchema.SchemaKind.Map
                    isMapArr = True
                    typeId = (CInt(smat) - CInt(SMAT_MAP_ARR_BASE_TAG)) + 1

                Case SMAT_ARR_BASE_TAG To SMAT_ARR_MAX_TAG
                    s.Kind = JsonSchema.SchemaKind.Array
                    typeId = (CInt(smat) - CInt(SMAT_ARR_BASE_TAG)) + 1

                Case Else
                    Throw New Exception($"Invalid non-object SMAT: 0x{smat:X2}")

            End Select

            If typeId < SMAT_TYPE_MIN_ID OrElse typeId > SMAT_TYPE_MAX_ID Then
                Throw New Exception("Invalid SMAT typeId: " & typeId)
            End If

            Dim baseType As JsonFieldType = CType(typeId, JsonFieldType)
            Dim schemaRef As Integer = -1

            If baseType = JsonFieldType.Object Then
                schemaRef = R.ReadSchemaPointerIndex()
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

            Return s

        End Function

        Private Function ReadObjectSchema(firstSmat As Byte) As SchemaDef

            Dim fieldCount As Integer = ReadSmatObjectFieldCountFromFirst(firstSmat)

            Dim s As New SchemaDef With {
                .Kind = JsonSchema.SchemaKind.Object
            }

            If fieldCount = 0 Then
                s.Fields = Array.Empty(Of FieldDef)()
                Return s
            End If

            Dim fields(fieldCount - 1) As FieldDef

            For f As Integer = 0 To fieldCount - 1

                Dim typeCode As JsonFieldType = CType(R.ReadByte(), JsonFieldType)

                Dim fieldName As String = Nothing
                Dim namePtr As Integer = -1
                ReadNameChunk(fieldName, namePtr)

                If fieldName Is Nothing AndAlso namePtr >= 0 Then
                    If _strings Is Nothing OrElse namePtr >= _strings.Length Then
                        Throw New Exception("Invalid field-name string pointer index: " & namePtr)
                    End If
                    fieldName = _strings(namePtr)
                End If

                If fieldName Is Nothing Then
                    Throw New Exception("Field name cannot be null.")
                End If

                Dim schemaIndex As Integer = -1
                If BaseType(typeCode) = JsonFieldType.Object Then
                    schemaIndex = R.ReadSchemaPointerIndex()
                End If

                fields(f) = New FieldDef With {
                    .Name = fieldName,
                    .NamePtrIndex = -1,
                    .TypeCode = typeCode,
                    .SchemaIndex = schemaIndex
                }

            Next

            s.Fields = fields
            Return s

        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadSmatObjectFieldCountFromFirst(first As Byte) As Integer

            Select Case first
                Case 0 To SMAT_OBJ_LITERAL_MAX_FIELDS : Return CInt(first)
                Case SMAT_OBJ_LUINT_TAG : Return R.CheckedCount(R.ReadLUINT(), 2, "Object fieldCount")
                Case SMAT_NULL_TAG : Throw New Exception("SMAT NULL cannot be used as an object schema header.")
                Case Else : Throw New Exception($"Invalid SMAT for object schema: 0x{first:X2}")
            End Select

        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Sub ReadNameChunk(ByRef literalName As String, ByRef ptrIndex As Integer)

            Dim isNull As Boolean
            R.ReadDSTRChunk(literalName, ptrIndex, isNull)

            If isNull Then
                Throw New Exception("Field name cannot be null.")
            End If

        End Sub

        Private Sub ResolveSchemaNames()

            If _schemas Is Nothing OrElse _schemas.Length = 0 Then Return

            For Each s As SchemaDef In _schemas

                If s Is Nothing Then Continue For
                If s.Kind <> JsonSchema.SchemaKind.Object Then Continue For
                If s.Fields Is Nothing OrElse s.Fields.Length = 0 Then Continue For

                For i As Integer = 0 To s.Fields.Length - 1
                    If String.IsNullOrEmpty(s.Fields(i).Name) Then
                        Throw New Exception("Field name cannot be null.")
                    End If
                Next

            Next

        End Sub

#End Region

#Region "[data]"

        Private Function ReadObjectValue(expected As SchemaDef) As BToken

            Dim marker As Byte = R.ReadByte()

            Select Case marker

                Case SOBJ_NULL_TAG
                    Return BNull.Instance(JsonFieldType.Object)

                Case SOBJ_PRESENT_EXPECTED_SCHEMA
                    Return ReadSchemaBody(expected)

                Case SOBJ_SCHEMA_PTR_1B_TAG To SOBJ_SCHEMA_PTR_3B_TAG
                    Dim idx As Integer = ReadSchemaPointerIndexFromFirst(marker)

                    If idx < 0 OrElse idx >= _schemas.Length Then
                        Throw New Exception("Invalid schema override index: " & idx)
                    End If

                    If _schemas(idx) Is Nothing Then
                        Throw New Exception("Schema override points to NULL entry: " & idx)
                    End If

                    Return ReadSchemaBody(_schemas(idx))

                Case Else

                    Dim typeCode As JsonFieldType = CType(marker, JsonFieldType)
                    Dim valueBaseType As JsonFieldType = BaseType(typeCode)

                    If valueBaseType = JsonFieldType.Object Then
                        If Not IsArray(typeCode) Then
                            Throw New Exception("Invalid SOBJ marker: Object scalar override is not allowed.")
                        End If
                        Return ReadObjectArrayOverrideExpected(expected)
                    End If

                    If IsArray(typeCode) Then
                        Return ReadArray(valueBaseType, -1)
                    End If

                    Return ReadScalar(valueBaseType, -1)

            End Select

        End Function

        Private Function ReadSchemaBody(schema As SchemaDef) As BToken

            If schema Is Nothing Then
                Return BNull.Instance(JsonFieldType.Unknown)
            End If

            Select Case schema.Kind

                Case JsonSchema.SchemaKind.Map
                    Return ReadMapBody(schema)

                Case JsonSchema.SchemaKind.Array
                    Return ReadArray(schema.ArrayElemType, schema.ArrayElemSchemaIndex)

                Case Else

                    Dim obj As New BObject()

                    If schema.Fields Is Nothing OrElse schema.Fields.Length = 0 Then
                        Return obj
                    End If

                    For i As Integer = 0 To schema.Fields.Length - 1
                        Dim f As FieldDef = schema.Fields(i)
                        obj(f.Name) = ReadValue(f.TypeCode, f.SchemaIndex)
                    Next

                    Return obj

            End Select

        End Function

        Private Function ReadSchemaPointerIndexFromFirst(firstTag As Byte) As Integer

            Select Case firstTag
                Case SOBJ_SCHEMA_PTR_1B_TAG
                    Return CInt(R.ReadByte())

                Case SOBJ_SCHEMA_PTR_2B_TAG
                    Return (CInt(R.ReadByte()) << 8) Or CInt(R.ReadByte())

                Case SOBJ_SCHEMA_PTR_3B_TAG
                    Return (CInt(R.ReadByte()) << 16) Or
                           (CInt(R.ReadByte()) << 8) Or
                            CInt(R.ReadByte())

                Case Else
                    Throw New Exception($"Invalid schema pointer tag: 0x{firstTag:X2}")
            End Select

        End Function

        Private Function ReadObjectArrayOverrideExpected(expected As SchemaDef) As BToken

            Dim isNull As Boolean
            Dim countValue As Long = R.ReadLUINTOrNull(isNull)

            If isNull Then
                Return CreateTypedArrayNull(JsonFieldType.Object)
            End If

            Dim count As Integer = R.CheckedCount(countValue, 1, "Object[] count")
            Dim arr As New BArray(JsonFieldType.Object)

            For i As Integer = 0 To count - 1
                arr.Add(ReadObjectValue(expected))
            Next

            Return arr

        End Function

        Private Function ReadMapBody(schema As SchemaDef) As BObject

            Dim count As Integer = R.CheckedCount(R.ReadLUINT(), 2, "Map count")
            Dim obj As New BObject(BObject.Enum_ObjectKind.Map, schema.MapValueType)

            For i As Integer = 0 To count - 1
                Dim key As String = ReadDSTRValue()
                Dim value As BToken = ReadValue(schema.MapValueType, schema.MapValueSchemaIndex)
                obj(If(key, "null")) = value
            Next

            Return obj

        End Function

        Private Function ReadValue(typeCode As JsonFieldType, schemaIndex As Integer) As BToken
            If IsArray(typeCode) Then Return ReadArray(BaseType(typeCode), schemaIndex)
            Return ReadScalar(BaseType(typeCode), schemaIndex)
        End Function

        Private Function ReadArray(baseType As JsonFieldType, schemaIndex As Integer) As BToken

            Dim isNull As Boolean
            Dim countValue As Long = R.ReadLUINTOrNull(isNull)

            If isNull Then
                Return CreateTypedArrayNull(baseType)
            End If

            Dim count As Integer

            Select Case baseType
                Case JsonFieldType.Integer : count = R.CheckedCount(countValue, 1, "Integer[] count")
                Case JsonFieldType.Float4Bytes : count = R.CheckedCount(countValue, 4, "Float4[] count")
                Case JsonFieldType.Float8Bytes : count = R.CheckedCount(countValue, 8, "Float8[] count")
                Case JsonFieldType.Boolean : count = R.CheckedCount(countValue, 1, "Boolean[] count")
                Case JsonFieldType.Date : count = R.CheckedCount(countValue, 1, "Date[] count")
                Case JsonFieldType.String : count = R.CheckedCount(countValue, 1, "String[] count")
                Case JsonFieldType.Bytes : count = R.CheckedCount(countValue, 1, "Bytes[] count")
                Case JsonFieldType.Object : count = R.CheckedCount(countValue, 1, "Object[] count")
                Case Else : Throw New NotSupportedException("Unsupported array base type: " & baseType.ToString())
            End Select

            Dim arr As New BArray(baseType)

            Select Case baseType

                Case JsonFieldType.Integer
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Long = R.ReadLINTOrNull(n)
                        arr.Add(If(n, CreateTypedNull(JsonFieldType.Integer), CType(New BNumber(v), BToken)))
                    Next

                Case JsonFieldType.Float4Bytes
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Single = R.ReadSingleOrNull(n)
                        arr.Add(If(n, CreateTypedNull(JsonFieldType.Float4Bytes), CType(New BNumber(v), BToken)))
                    Next

                Case JsonFieldType.Float8Bytes
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Double = R.ReadDoubleOrNull(n)
                        arr.Add(If(n, CreateTypedNull(JsonFieldType.Float8Bytes), CType(New BNumber(v), BToken)))
                    Next

                Case JsonFieldType.Boolean
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Boolean = R.ReadBoolOrNull(n)
                        arr.Add(If(n, CreateTypedNull(JsonFieldType.Boolean), CType(New BBoolean(v), BToken)))
                    Next

                Case JsonFieldType.Date
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As DateTime = ReadDateValueUtcOrNull(n)
                        arr.Add(If(n, CreateTypedNull(JsonFieldType.Date), CType(New BDate(v), BToken)))
                    Next

                Case JsonFieldType.String
                    For i As Integer = 0 To count - 1
                        Dim s As String = ReadDSTRValue()
                        arr.Add(If(s Is Nothing, CreateTypedNull(JsonFieldType.String), CType(New BString(s), BToken)))
                    Next

                Case JsonFieldType.Bytes
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim b() As Byte = R.ReadBarrOrNull(n)
                        arr.Add(If(n, CreateTypedNull(JsonFieldType.Bytes), CType(New BBytes(b), BToken)))
                    Next

                Case JsonFieldType.Object

                    If schemaIndex < 0 OrElse schemaIndex >= _schemas.Length Then
                        Throw New Exception("Invalid expected schema index for Object[]: " & schemaIndex)
                    End If

                    If _schemas(schemaIndex) Is Nothing Then
                        Throw New Exception("Object[] expected schema points to NULL entry: " & schemaIndex)
                    End If

                    Dim expected As SchemaDef = _schemas(schemaIndex)

                    For i As Integer = 0 To count - 1
                        arr.Add(ReadObjectValue(expected))
                    Next

                Case Else
                    Throw New NotSupportedException("Unsupported array base type: " & baseType.ToString())

            End Select

            Return arr

        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadDateValueUtcOrNull(ByRef isNull As Boolean) As DateTime

            Dim literalUtc As DateTime
            Dim ptrIndex As Integer

            R.ReadDDate(literalUtc, ptrIndex, isNull)

            If isNull Then
                Return DateTime.MinValue
            End If

            If ptrIndex >= 0 Then
                If _dates Is Nothing OrElse ptrIndex >= _dates.Length Then
                    Throw New Exception("Invalid DDATE pointer index: " & ptrIndex)
                End If
                Return _dates(ptrIndex)
            End If

            Return literalUtc

        End Function

        Private Function ReadScalar(baseType As JsonFieldType, schemaIndex As Integer) As BToken

            Select Case baseType

                Case JsonFieldType.Integer
                    Dim n As Boolean
                    Dim v As Long = R.ReadLINTOrNull(n)
                    Return If(n, CreateTypedNull(JsonFieldType.Integer), CType(New BNumber(v), BToken))

                Case JsonFieldType.Boolean
                    Dim n As Boolean
                    Dim v As Boolean = R.ReadBoolOrNull(n)
                    Return If(n, CreateTypedNull(JsonFieldType.Boolean), CType(New BBoolean(v), BToken))

                Case JsonFieldType.Float4Bytes
                    Dim n As Boolean
                    Dim v As Single = R.ReadSingleOrNull(n)
                    Return If(n, CreateTypedNull(JsonFieldType.Float4Bytes), CType(New BNumber(v), BToken))

                Case JsonFieldType.Float8Bytes
                    Dim n As Boolean
                    Dim v As Double = R.ReadDoubleOrNull(n)
                    Return If(n, CreateTypedNull(JsonFieldType.Float8Bytes), CType(New BNumber(v), BToken))

                Case JsonFieldType.Date
                    Dim n As Boolean
                    Dim v As DateTime = ReadDateValueUtcOrNull(n)
                    Return If(n, CreateTypedNull(JsonFieldType.Date), CType(New BDate(v), BToken))

                Case JsonFieldType.String
                    Dim s As String = ReadDSTRValue()
                    Return If(s Is Nothing, CreateTypedNull(JsonFieldType.String), CType(New BString(s), BToken))

                Case JsonFieldType.Bytes
                    Dim n As Boolean
                    Dim b() As Byte = R.ReadBarrOrNull(n)
                    Return If(n, CreateTypedNull(JsonFieldType.Bytes), CType(New BBytes(b), BToken))

                Case JsonFieldType.Object
                    If schemaIndex < 0 OrElse schemaIndex >= _schemas.Length Then
                        Throw New Exception("Invalid expected schema index for Object: " & schemaIndex)
                    End If

                    If _schemas(schemaIndex) Is Nothing Then
                        Throw New Exception("Object expected schema points to NULL entry: " & schemaIndex)
                    End If

                    Return ReadObjectValue(_schemas(schemaIndex))

                Case Else
                    Throw New NotSupportedException("Unsupported scalar FieldType: " & baseType.ToString())

            End Select

        End Function

        Private Function ReadDSTRValue() As String

            Dim literal As String = Nothing
            Dim ptrIndex As Integer = -1
            Dim isNull As Boolean = False

            R.ReadDSTRChunk(literal, ptrIndex, isNull)

            If isNull Then
                Return Nothing
            End If

            If ptrIndex >= 0 Then
                If _strings Is Nothing OrElse ptrIndex >= _strings.Length Then
                    Throw New Exception("Invalid DSTR pointer index: " & ptrIndex)
                End If
                Return _strings(ptrIndex)
            End If

            Return literal

        End Function

#End Region

#Region "[files]"

        ''' <summary>
        ''' Reads the FILES zone into an ordered list of file entries.
        ''' </summary>
        ''' <remarks>
        ''' Wire layout:
        ''' [LUINT filesByteLength][LUINT fileCount][file0][file1]...[fileN]
        ''' 
        ''' Each file entry is encoded as:
        ''' [LSTR literal fileName][LUINT byteLength][raw bytes]
        ''' 
        ''' Notes:
        ''' - File names must be non-null and non-empty.
        ''' - File payloads cannot be null.
        ''' - Duplicate file names are rejected.
        ''' - filesByteLength covers the complete serialized FILES payload
        '''   that follows it, including the LUINT fileCount and all file entries.
        ''' </remarks>
        Private Sub ReadFilesZone(ByRef filesOut As List(Of KeyValuePair(Of String, Byte())))

            Dim declaredBodyLength As Integer = R.CheckedLength(R.ReadLUINT(), "Files body length")

            If declaredBodyLength < 1 Then
                Throw New Exception("FILES body length cannot be 0. It must include the serialized fileCount.")
            End If

            Dim startPos As Integer = R.Position()

            Dim count As Integer = R.CheckedCount(R.ReadLUINT(), 1, "Files count")

            If count = 0 Then
                Throw New Exception("FILES zone must be omitted when fileCount = 0.")
            End If

            Dim list As New List(Of KeyValuePair(Of String, Byte()))(count)
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
                Dim fileData() As Byte = R.ReadBarrOrNull(isNull)

                If isNull Then
                    Throw New Exception("FILES zone cannot contain NULL file payloads.")
                End If

                list.Add(New KeyValuePair(Of String, Byte())(fileName, fileData))

            Next

            Dim actualBodyLength As Integer = R.Position() - startPos

            If actualBodyLength <> declaredBodyLength Then
                Throw New Exception($"FILES body length mismatch. Declared={declaredBodyLength}, actual={actualBodyLength}.")
            End If

            filesOut = list

        End Sub

        ''' <summary>
        ''' Reads a literal LSTR value and rejects null.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadLstrLiteralNonNull(label As String) As String

            Dim isNull As Boolean
            Dim len As Long = R.ReadLUINTOrNull(isNull)

            If isNull Then
                Throw New Exception(label & " cannot be null.")
            End If

            Return R.ReadUtf8Literal(R.CheckedLength(len, label & " length"))

        End Function

#End Region

#Region "FieldType helpers"

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function IsArray(typeCode As JsonFieldType) As Boolean
            Return JsonField.FieldTypeIsArray(typeCode)
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function BaseType(typeCode As JsonFieldType) As JsonFieldType
            Return JsonField.FieldTypeWithoutArrayFlag(typeCode)
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function CreateTypedArrayNull(baseType As JsonFieldType) As BToken
            Return BNull.Instance(CType(CInt(baseType) Or CInt(JsonFieldType.ArrayFlag), JsonFieldType))
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function CreateTypedNull(baseType As JsonFieldType) As BToken
            Return BNull.Instance(baseType)
        End Function

#End Region

    End Class

End Namespace