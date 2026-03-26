Imports System.Runtime.CompilerServices

''' <summary>
''' Defines the wire-level constants, tag ranges, null sentinels, and tiny immutable caches
''' used by the Bytery binary format.
''' </summary>
''' <remarks>
''' This module is part of the protocol definition itself.
''' Any change here may affect the on-wire format and compatibility between encoder and decoder.
''' </remarks>
Public Module Constants

#Region "FILE HEADER (container v1)"

    ' =========================================================================
    ' FILE HEADER
    ' =========================================================================
    '
    ' Layout:
    '   [4 bytes magic][1 byte version][zone mask chain][present zones...]
    '
    ' Canonical zone order:
    '   [header][files][string table][date table][schema table][data]
    '
    ' Magic:
    '   "BYT1" = 0x42 0x59 0x54 0x31
    '
    ' Notes:
    '   - The magic identifies the payload as a Bytery container.
    '   - The version byte allows the container format to evolve over time.
    '   - The ZMSK chain declares which top-level zones are present.
    '   - Zones always keep their canonical protocol order; absent zones are
    '     simply omitted from the payload.
    '
    Friend Const FILE_MAGIC_B0 As Byte = &H42
    Friend Const FILE_MAGIC_B1 As Byte = &H59
    Friend Const FILE_MAGIC_B2 As Byte = &H54
    Friend Const FILE_MAGIC_B3 As Byte = &H31

    Friend Const FILE_VERSION_V1 As Byte = 1

#End Region

#Region "ZMSK - Zone Mask (extensible container zone-presence chain)"

    ' =========================================================================
    ' ZMSK
    ' =========================================================================
    '
    ' Purpose:
    '   Extensible presence-mask chain that declares which top-level container
    '   zones are present in the current payload.
    '
    ' Position:
    '   Written immediately after [4 bytes magic][1 byte version].
    '
    ' Layout:
    '   [zmsk0][zmsk1?][zmsk2?]...
    '
    ' Per-byte semantics:
    '   bits 0..6 => zone-presence flags assigned to that byte
    '   bit  7    => continuation flag; when set, another ZMSK byte follows
    '
    ' Canonical zone order:
    '   After the ZMSK chain, zones are written in protocol-defined order,
    '   skipping every zone whose presence bit is not set.
    '
    ' First ZMSK byte mapping (v1):
    '   bit 0 => header
    '   bit 1 => files
    '   bit 2 => string table
    '   bit 3 => date table
    '   bit 4 => schema table
    '   bit 5 => data
    '   bit 6 => reserved for future first-byte zone
    '   bit 7 => continuation flag
    '
    ' Extension rule:
    '   Each additional ZMSK byte contributes 7 more zone bits.
    '   The continuation bit is never itself a zone.
    '
    ' Notes:
    '   - ZMSK describes presence only; it does not reorder zones.
    '   - Decoder must read ZMSK bytes until a byte with no continuation flag
    '     is found.
    '   - Leaving bit 6 unused in the first byte preserves one extra v1-era
    '     zone slot before a second mask byte becomes necessary.
    '
    Friend Const ZMSK_ZONE_BITS_MASK As Byte = &B1111111
    Friend Const ZMSK_HAS_NEXT As Byte = &B10000000

    Friend Const ZMSK_BITS_PER_BYTE As Integer = 8
    Friend Const ZMSK_ZONE_BITS_PER_BYTE As Integer = 7

    Friend Const ZMSK_HEADERS As Byte = &B1
    Friend Const ZMSK_FILES As Byte = &B10
    Friend Const ZMSK_STRING_TABLE As Byte = &B100
    Friend Const ZMSK_DATE_TABLE As Byte = &B1000
    Friend Const ZMSK_SCHEMA_TABLE As Byte = &B10000
    Friend Const ZMSK_DATA As Byte = &B100000

    Friend Const ZMSK_RESERVED_6 As Byte = &B1000000

    Friend Const ZMSK_V1_DEFINED_ZONES_MASK As Byte = &B111111

#End Region

#Region "U8_PAYLOAD_CACHE - Cached single-byte payload arrays (0..255)"

    ' =========================================================================
    ' U8_PAYLOAD_CACHE
    ' =========================================================================
    '
    ' Purpose:
    '   Avoid repeated allocation of tiny one-byte arrays for common payload cases.
    '
    ' Shape:
    '   cache(value) => { value }
    '
    ' Range:
    '   0..255
    '
    ' Contract:
    '   - Entries are shared and must be treated as immutable.
    '   - Safe to reuse anywhere a single raw byte payload is needed.
    '
    Friend ReadOnly U8_PAYLOAD_CACHE As Byte()() = BuildU8PayloadCache()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildU8PayloadCache() As Byte()()
        Dim cache(255)() As Byte
        For i As Integer = 0 To 255
            cache(i) = {CByte(i)}
        Next
        Return cache
    End Function

#End Region

#Region "LUINT - Literal Unsigned Integer (nullable)"

    ' =========================================================================
    ' LUINT
    ' =========================================================================
    '
    ' Purpose:
    '   Unsigned integer codec used for infrastructure values such as:
    '   - counts
    '   - lengths
    '   - table indexes
    '   - other non-negative protocol metadata
    '
    ' Encoding:
    '   0..246   => literal value 0..246                       (1 byte total)
    '   247      => value = 247 + nextByte                    (2 bytes total, range 247..502)
    '   248      => UInt16BE payload                          (3 bytes total)
    '   249      => UInt24BE payload                          (4 bytes total)
    '   250      => UInt32BE payload                          (5 bytes total)
    '   251      => UInt40BE payload                          (6 bytes total)
    '   252      => UInt48BE payload                          (7 bytes total)
    '   253      => UInt56BE payload                          (8 bytes total)
    '   254      => UInt64BE payload                          (9 bytes total)
    '   255      => NULL                                      (1 byte total)
    '
    ' Canonical rule:
    '   - Values 0..246 must use the literal form.
    '   - Values 247..502 must use the compact [247][u8] form.
    '   - Wider forms are only used when smaller forms cannot represent the value.
    '
    ' Important:
    '   LUINT is an infrastructure codec. It is not the same as LINT.
    '
    Friend Const LUINT_B0_MAX As Integer = 246

    Friend Const LUINT_B8 As Byte = 247
    Friend Const LUINT_B8_BASE_VALUE As Integer = LUINT_B0_MAX + 1
    Friend Const LUINT_B8_MAX As Integer = LUINT_B8_BASE_VALUE + 255

    Friend Const LUINT_16 As Byte = 248
    Friend Const LUINT_24 As Byte = 249
    Friend Const LUINT_32 As Byte = 250
    Friend Const LUINT_40 As Byte = 251
    Friend Const LUINT_48 As Byte = 252
    Friend Const LUINT_56 As Byte = 253
    Friend Const LUINT_64 As Byte = 254

    Friend Const LUINT_NULL As Byte = 255

    ' =========================================================================
    ' LUINT small encode cache
    ' =========================================================================
    '
    ' Coverage:
    '   0..502
    '
    ' Mapping:
    '   0..246   => { value }
    '   247..502 => { 247, value - 247 }
    '
    ' Use this cache on hot paths to avoid allocating tiny arrays for the most
    ' common LUINT values.
    '
    Friend ReadOnly LUINT_ENCODE_0_TO_502 As Byte()() = BuildLUIntEncodeCache()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildLUIntEncodeCache() As Byte()()
        Dim cache(LUINT_B8_MAX)() As Byte

        For v As Integer = 0 To LUINT_B8_MAX
            If v <= LUINT_B0_MAX Then
                cache(v) = U8_PAYLOAD_CACHE(v)
            Else
                cache(v) = {LUINT_B8, CByte(v - LUINT_B8_BASE_VALUE)}
            End If
        Next

        Return cache
    End Function

#End Region

#Region "LINT - Literal Signed Integer (nullable)"

    ' =========================================================================
    ' LINT
    ' =========================================================================
    '
    ' Purpose:
    '   Signed integer VALUE codec used for schema fields of type Integer.
    '
    ' This is a value codec, not a length/count codec.
    '
    ' Encoding:
    '   0..219     => literal positive values 0..219                  (1 byte)
    '   220..238   => literal negative values -1..-19                (1 byte)
    '                  value = -(tag - 219)
    '   239        => positive compact: 220 + nextByte               (2 bytes)
    '                  range 220..475
    '   240..246   => positive big-endian magnitude                  (3..9 bytes total)
    '                  240=U16, 241=U24, ..., 246=U64
    '   247        => negative compact: -(20 + nextByte)            (2 bytes)
    '                  range -20..-275
    '   248..254   => negative big-endian magnitude                  (3..9 bytes total)
    '                  payload is magnitude, not two's complement
    '   255        => NULL
    '
    ' Canonical rule:
    '   Always use the shortest available representation.
    '
    ' Special note:
    '   Long.MinValue is represented through the negative U64 path using
    '   magnitude 2^63.
    '
    Friend Const LINT_POS_LITERAL_MAX_VALUE As Integer = 219

    Friend Const LINT_NEG_LITERAL_FIRST_TAG As Byte = 220
    Friend Const LINT_NEG_LITERAL_LAST_TAG As Byte = 238
    Friend Const LINT_NEG_LITERAL_COUNT As Integer =
        (LINT_NEG_LITERAL_LAST_TAG - LINT_NEG_LITERAL_FIRST_TAG + 1)

    Friend Const LINT_POS_PLUS_U8_BASE_VALUE As Integer = 220
    Friend Const LINT_POS_PLUS_U8_MAX_VALUE As Integer =
        LINT_POS_PLUS_U8_BASE_VALUE + 255

    Friend Const LINT_POS_PLUS_U8_TAG As Byte = 239

    Friend Const LINT_POS_U16_TAG As Byte = 240
    Friend Const LINT_POS_U24_TAG As Byte = 241
    Friend Const LINT_POS_U32_TAG As Byte = 242
    Friend Const LINT_POS_U40_TAG As Byte = 243
    Friend Const LINT_POS_U48_TAG As Byte = 244
    Friend Const LINT_POS_U56_TAG As Byte = 245
    Friend Const LINT_POS_U64_TAG As Byte = 246

    Friend Const LINT_NEG_PLUS_U8_BASE_MAG As Integer = LINT_NEG_LITERAL_COUNT + 1
    Friend Const LINT_NEG_PLUS_U8_MAX_MAG As Integer = LINT_NEG_PLUS_U8_BASE_MAG + 255

    Friend Const LINT_NEG_PLUS_U8_TAG As Byte = 247

    Friend Const LINT_NEG_U16_TAG As Byte = 248
    Friend Const LINT_NEG_U24_TAG As Byte = 249
    Friend Const LINT_NEG_U32_TAG As Byte = 250
    Friend Const LINT_NEG_U40_TAG As Byte = 251
    Friend Const LINT_NEG_U48_TAG As Byte = 252
    Friend Const LINT_NEG_U56_TAG As Byte = 253
    Friend Const LINT_NEG_U64_TAG As Byte = 254

    Friend Const LINT_NULL_TAG As Byte = 255

    Friend ReadOnly LINT_CHUNK_NULL As Byte() = {LINT_NULL_TAG}

    ' =========================================================================
    ' LINT small encode caches
    ' =========================================================================
    '
    ' Positive cache:
    '   0..475
    '   - 0..219   => literal
    '   - 220..475 => compact [239][value-220]
    '
    ' Negative cache:
    '   magnitude 1..275
    '   - 1..19    => literal negative tags 220..238
    '   - 20..275  => compact [247][magnitude-20]
    '
    ' These caches cover the most common integer values with zero tiny-array
    ' allocations.
    '
    Friend ReadOnly LINT_ENCODE_POS_0_TO_475 As Byte()() = BuildLIntPositiveEncodeCache()
    Friend ReadOnly LINT_ENCODE_NEG_MAG_1_TO_275 As Byte()() = BuildLIntNegativeMagnitudeEncodeCache()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildLIntPositiveEncodeCache() As Byte()()
        Dim cache(LINT_POS_PLUS_U8_MAX_VALUE)() As Byte

        For v As Integer = 0 To LINT_POS_PLUS_U8_MAX_VALUE
            If v <= LINT_POS_LITERAL_MAX_VALUE Then
                cache(v) = U8_PAYLOAD_CACHE(v)
            Else
                cache(v) = {LINT_POS_PLUS_U8_TAG, CByte(v - LINT_POS_PLUS_U8_BASE_VALUE)}
            End If
        Next

        Return cache
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildLIntNegativeMagnitudeEncodeCache() As Byte()()
        Dim maxMag As Integer = LINT_NEG_PLUS_U8_MAX_MAG

        Dim cache(maxMag)() As Byte
        cache(0) = Array.Empty(Of Byte)()

        For mag As Integer = 1 To maxMag
            If mag <= LINT_NEG_LITERAL_COUNT Then
                Dim tag As Integer = CInt(LINT_NEG_LITERAL_FIRST_TAG) + (mag - 1)
                cache(mag) = U8_PAYLOAD_CACHE(tag)
            Else
                cache(mag) = {LINT_NEG_PLUS_U8_TAG, CByte(mag - LINT_NEG_PLUS_U8_BASE_MAG)}
            End If
        Next

        Return cache
    End Function

#End Region

#Region "LSTR - Literal String (nullable, length is LUINT)"

    ' =========================================================================
    ' LSTR
    ' =========================================================================
    '
    ' Purpose:
    '   Literal UTF-8 string codec used when no string-pointer behavior is needed.
    '
    ' Layout:
    '   [LUINT byteLengthOrNull][raw UTF-8 bytes]
    '
    ' Null:
    '   LUINT_NULL => string is null and no payload bytes follow.
    '
    Friend ReadOnly LSTR_CHUNK_NULL_VALUE As New PTR({LUINT_NULL}, Array.Empty(Of Byte)())

#End Region

#Region "DSTR - Dynamic String (nullable, literal UTF-8 OR session pointer) [LEN_LIT=157, PTR_LIT=90]"

    ' =========================================================================
    ' DSTR
    ' =========================================================================
    '
    ' Purpose:
    '   Dynamic UTF-8 string codec used in data/schema paths where a value may be
    '   written either:
    '   - inline as literal UTF-8
    '   - as a pointer into the session StringTable
    '   - as null
    '
    ' Layout:
    '   0..156     => literal length = tag                        (157 literal lengths)
    '   157..246   => literal pointer index = tag - 157           (90 inline pointer tags)
    '   247        => length = 157 + nextByte                     (157..412)
    '   248        => length = UInt16BE
    '   249        => length = UInt24BE
    '   250        => length = UInt32BE
    '   251        => pointer index via compact U8 window
    '   252        => pointer index = UInt16BE
    '   253        => pointer index = UInt24BE
    '   254        => pointer index = UInt32BE
    '   255        => NULL
    '
    ' Pointer rules:
    '   - Literal pointer tags cover indexes 0..89.
    '   - Compact U8 pointer covers indexes 90..345 as [251][index-90].
    '
    ' Notes:
    '   - DSTR is session-dependent because pointer values refer to the current
    '     StringTable.
    '   - Literal and pointer forms intentionally share the same family so the
    '     first byte fully describes what follows.
    '
    Friend Const DSTR_LEN_LITERAL_MIN_TAG As Byte = 0
    Friend Const DSTR_LEN_LITERAL_MAX_TAG As Byte = 156
    Friend Const DSTR_LEN_LITERAL_MAX As Integer = 156

    Friend Const DSTR_PTR_LITERAL_BASE_TAG As Byte = 157
    Friend Const DSTR_PTR_LITERAL_COUNT As Integer = 90
    Friend Const DSTR_PTR_LITERAL_MIN_TAG As Byte = 157
    Friend Const DSTR_PTR_LITERAL_MAX_TAG As Byte = 246

    Friend Const DSTR_LEN_U8_TAG As Byte = 247
    Friend Const DSTR_LEN_U16_TAG As Byte = 248
    Friend Const DSTR_LEN_U24_TAG As Byte = 249
    Friend Const DSTR_LEN_U32_TAG As Byte = 250

    Friend Const DSTR_LEN_U8_BASE As Integer = DSTR_LEN_LITERAL_MAX + 1
    Friend Const DSTR_LEN_U8_MAX As Integer = DSTR_LEN_U8_BASE + 255

    Friend Const DSTR_PTR_U8_TAG As Byte = 251
    Friend Const DSTR_PTR_U16_TAG As Byte = 252
    Friend Const DSTR_PTR_U24_TAG As Byte = 253
    Friend Const DSTR_PTR_U32_TAG As Byte = 254

    Friend ReadOnly DSTR_PTR_U8_CHUNK As Byte() = {DSTR_PTR_U8_TAG}
    Friend ReadOnly DSTR_PTR_U16_CHUNK As Byte() = {DSTR_PTR_U16_TAG}
    Friend ReadOnly DSTR_PTR_U24_CHUNK As Byte() = {DSTR_PTR_U24_TAG}
    Friend ReadOnly DSTR_PTR_U32_CHUNK As Byte() = {DSTR_PTR_U32_TAG}

    Friend Const DSTR_NULL_TAG As Byte = 255
    Friend ReadOnly DSTR_CHUNK_NULL As Byte() = {DSTR_NULL_TAG}
    Friend ReadOnly DSTR_CHUNK_NULL_VALUE As New PTR(DSTR_CHUNK_NULL, Array.Empty(Of Byte)())

    ' =========================================================================
    ' DSTR literal-length cache
    ' =========================================================================
    '
    ' Coverage:
    '   0..412
    '
    ' Mapping:
    '   0..156   => { len }
    '   157..412 => { 247, len - 157 }
    '
    Friend ReadOnly DSTR_LEN_ENCODE_0_TO_412 As Byte()() =
    BuildDstrLenEncodeCache_0_To_412()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildDstrLenEncodeCache_0_To_412() As Byte()()

        Const MAX As Integer = (DSTR_LEN_U8_BASE + 255)
        Dim cache(MAX)() As Byte

        For len As Integer = 0 To MAX
            If len <= DSTR_LEN_LITERAL_MAX Then
                cache(len) = U8_PAYLOAD_CACHE(len)
            Else
                cache(len) = {DSTR_LEN_U8_TAG, CByte(len - DSTR_LEN_U8_BASE)}
            End If
        Next

        Return cache

    End Function

    ' =========================================================================
    ' DSTR pointer cache
    ' =========================================================================
    '
    ' Coverage:
    '   0..345
    '
    ' Mapping:
    '   0..89    => {157 + index}                    (literal pointer tag, no payload)
    '   90..345  => {251}{index - 90}               (compact pointer form)
    '
    ' Returned as PTR:
    '   - len  = first-byte chunk
    '   - data = payload bytes, if any
    '
    Friend ReadOnly DSTR_PTR_ENCODE_0_TO_345 As PTR() =
    BuildDstrPtrEncodeCache_0_To_345()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildDstrPtrEncodeCache_0_To_345() As PTR()

        Const MAX As Integer = (DSTR_PTR_LITERAL_COUNT + 255)
        Dim cache(MAX) As PTR

        For idx As Integer = 0 To MAX

            If idx < DSTR_PTR_LITERAL_COUNT Then
                Dim tag As Integer = CInt(DSTR_PTR_LITERAL_BASE_TAG) + idx
                cache(idx) = New PTR(U8_PAYLOAD_CACHE(tag), Array.Empty(Of Byte)())
            Else
                cache(idx) = New PTR(DSTR_PTR_U8_CHUNK, U8_PAYLOAD_CACHE(idx - DSTR_PTR_LITERAL_COUNT))
            End If

        Next

        Return cache

    End Function

#End Region

#Region "BOOL - Boolean (nullable, 1-byte payload)"

    ' =========================================================================
    ' BOOL
    ' =========================================================================
    '
    ' Purpose:
    '   Fixed-size Boolean scalar codec.
    '
    ' Encoding:
    '   0 => False
    '   1 => True
    '   2 => NULL
    '
    ' Notes:
    '   - BOOL intentionally does not use 255 as null.
    '   - This compact 0/1/2 representation is the canonical wire form.
    '
    Friend Const BOOL_FALSE As Byte = 0
    Friend Const BOOL_TRUE As Byte = 1
    Friend Const BOOL_NULL As Byte = 2

#End Region

#Region "SOBJ - Session Object (nullable, schema or primitive overrides)"

    ' =========================================================================
    ' SOBJ
    ' =========================================================================
    '
    ' Purpose:
    '   Object-slot marker used when a schema field has base type Object.
    '
    ' A single byte tells the decoder how to interpret the slot:
    '   - null
    '   - object using the expected schema
    '   - object using a schema override
    '   - primitive scalar override
    '   - primitive array override
    '
    ' Layout:
    '   0            => object present, use expected schema
    '   1..7         => primitive scalar override using JsonFieldType base id
    '   0x80|1..7    => primitive array override using ArrayFlag | base id
    '   252          => schema override, schemaIndex follows as UInt8
    '   253          => schema override, schemaIndex follows as UInt16BE
    '   254          => schema override, schemaIndex follows as UInt24BE
    '   255          => NULL
    '
    ' Notes:
    '   - Tag 0 is reserved for "present with expected schema".
    '   - Schema override tags are session-dependent because schema indexes point
    '     into the current session SchemaTable.
    '
    Friend Const SOBJ_PRESENT_EXPECTED_SCHEMA As Byte = 0
    Friend Const SOBJ_NULL_TAG As Byte = 255

    Friend Const SOBJ_PRIMITIVE_MIN As Byte = Bytery.JSON.JsonFieldType.Integer
    Friend Const SOBJ_PRIMITIVE_MAX As Byte = Bytery.JSON.JsonFieldType.Bytes

    Friend Const SOBJ_ARRAY_FLAG As Byte = &H80

    Friend Const SOBJ_SCHEMA_PTR_1B_TAG As Byte = 252
    Friend Const SOBJ_SCHEMA_PTR_2B_TAG As Byte = 253
    Friend Const SOBJ_SCHEMA_PTR_3B_TAG As Byte = 254

    Friend ReadOnly SOBJ_SCHEMA_PTR_1B_CHUNK As Byte() = U8_PAYLOAD_CACHE(SOBJ_SCHEMA_PTR_1B_TAG)
    Friend ReadOnly SOBJ_SCHEMA_PTR_2B_CHUNK As Byte() = U8_PAYLOAD_CACHE(SOBJ_SCHEMA_PTR_2B_TAG)
    Friend ReadOnly SOBJ_SCHEMA_PTR_3B_CHUNK As Byte() = U8_PAYLOAD_CACHE(SOBJ_SCHEMA_PTR_3B_TAG)

    ' =========================================================================
    ' SOBJ schema-pointer U8 cache
    ' =========================================================================
    '
    ' Coverage:
    '   schema indexes 0..255
    '
    ' Mapping:
    '   {252}{schemaIndex}
    '
    Friend ReadOnly SOBJ_SCHEMA_PTR_U8_ENCODE_0_TO_255 As PTR() = BuildSObjSchemaPtrU8Cache()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildSObjSchemaPtrU8Cache() As PTR()
        Dim cache(255) As PTR
        For i As Integer = 0 To 255
            cache(i) = New PTR(SOBJ_SCHEMA_PTR_1B_CHUNK, U8_PAYLOAD_CACHE(i))
        Next
        Return cache
    End Function

#End Region

#Region "SMAT - Schema Type (1 byte header for schema table entries)"

    ' =========================================================================
    ' SMAT
    ' =========================================================================
    '
    ' Purpose:
    '   First byte of every SchemaTable entry.
    '
    ' Layout by range:
    '   255        => NULL schema entry
    '
    '   247..254   => ARRAY schema
    '                 247=Integer[] ... 254=Object[]
    '
    '   239..246   => MAP schema where value type is Array<T>
    '                 239=Map<String,Integer[]> ... 246=Map<String,Object[]>
    '
    '   231..238   => MAP schema where value type is T
    '                 231=Map<String,Integer> ... 238=Map<String,Object>
    '
    '   0..229     => OBJECT schema with literal field count
    '   230        => OBJECT schema with field count encoded as LUINT
    '
    ' Type id mapping:
    '   1=Integer
    '   2=Float4Bytes
    '   3=Float8Bytes
    '   4=Boolean
    '   5=Date
    '   6=String
    '   7=Bytes
    '   8=Object
    '
    Friend Const SMAT_NULL_TAG As Byte = 255

    Friend Const SMAT_OBJ_LITERAL_MAX_FIELDS As Integer = 229
    Friend Const SMAT_OBJ_LUINT_TAG As Byte = 230

    Friend Const SMAT_TYPE_MIN_ID As Integer = 1
    Friend Const SMAT_TYPE_MAX_ID As Integer = 8
    Friend Const SMAT_TYPE_COUNT As Integer = (SMAT_TYPE_MAX_ID - SMAT_TYPE_MIN_ID + 1)

    Friend Const SMAT_MAP_BASE_TAG As Byte = 231

    Friend Const SMAT_MAP_INTEGER As Byte = 231
    Friend Const SMAT_MAP_FLOAT4 As Byte = 232
    Friend Const SMAT_MAP_FLOAT8 As Byte = 233
    Friend Const SMAT_MAP_BOOL As Byte = 234
    Friend Const SMAT_MAP_DATE As Byte = 235
    Friend Const SMAT_MAP_STRING As Byte = 236
    Friend Const SMAT_MAP_BYTES As Byte = 237
    Friend Const SMAT_MAP_OBJECT As Byte = 238

    Friend Const SMAT_MAP_MIN_TAG As Byte = SMAT_MAP_INTEGER
    Friend Const SMAT_MAP_MAX_TAG As Byte = SMAT_MAP_OBJECT

    Friend Const SMAT_MAP_ARR_BASE_TAG As Byte = 239

    Friend Const SMAT_MAP_ARR_INTEGER As Byte = 239
    Friend Const SMAT_MAP_ARR_FLOAT4 As Byte = 240
    Friend Const SMAT_MAP_ARR_FLOAT8 As Byte = 241
    Friend Const SMAT_MAP_ARR_BOOL As Byte = 242
    Friend Const SMAT_MAP_ARR_DATE As Byte = 243
    Friend Const SMAT_MAP_ARR_STRING As Byte = 244
    Friend Const SMAT_MAP_ARR_BYTES As Byte = 245
    Friend Const SMAT_MAP_ARR_OBJECT As Byte = 246

    Friend Const SMAT_MAP_ARR_MIN_TAG As Byte = SMAT_MAP_ARR_INTEGER
    Friend Const SMAT_MAP_ARR_MAX_TAG As Byte = SMAT_MAP_ARR_OBJECT

    Friend Const SMAT_ARR_BASE_TAG As Byte = 247

    Friend Const SMAT_ARR_INTEGER As Byte = 247
    Friend Const SMAT_ARR_FLOAT4 As Byte = 248
    Friend Const SMAT_ARR_FLOAT8 As Byte = 249
    Friend Const SMAT_ARR_BOOL As Byte = 250
    Friend Const SMAT_ARR_DATE As Byte = 251
    Friend Const SMAT_ARR_STRING As Byte = 252
    Friend Const SMAT_ARR_BYTES As Byte = 253
    Friend Const SMAT_ARR_OBJECT As Byte = 254

    Friend Const SMAT_ARR_MIN_TAG As Byte = SMAT_ARR_INTEGER
    Friend Const SMAT_ARR_MAX_TAG As Byte = SMAT_ARR_OBJECT

#End Region

#Region "LDATE - Literal DateTime (8-byte ticks BE, no pointers)"

    ' =========================================================================
    ' LDATE
    ' =========================================================================
    '
    ' Purpose:
    '   Literal DateTime representation used exclusively inside the DateTable.
    '
    ' Layout:
    '   [8 bytes UTC ticks, big-endian]
    '
    ' Important invariant:
    '   For any valid DateTime ticks value, the first big-endian byte is in the
    '   range 0x00..0x2B. DDATE relies on this fact to reserve 0x2C..0xFF for
    '   pointer and null tags without colliding with inline ticks.
    '
    Friend Const LDATE_TICKS_BYTE_COUNT As Integer = 8
    Friend Const LDATE_TICKS_B0_MAX As Byte = &H2B

#End Region

#Region "DDATE - Dynamic Date (nullable, inline ticks OR session pointer) [PTR_LIT starts at 0x2C]"

    ' =========================================================================
    ' DDATE
    ' =========================================================================
    '
    ' Purpose:
    '   Dynamic DateTime codec used in data paths where a date may be written:
    '   - inline as raw UTC ticks
    '   - as a pointer into the session DateTable
    '   - as null
    '
    ' Layout:
    '   0x00..0x2B  => inline ticks; first byte is ticks[0], then 7 more bytes
    '   0x2C..0xFA  => literal pointer index 0..206
    '   0xFB        => compact pointer U8, index = 207 + nextByte
    '   0xFC        => pointer index = UInt16BE
    '   0xFD        => pointer index = UInt24BE
    '   0xFE        => pointer index = UInt32BE
    '   0xFF        => NULL
    '
    ' Canonical pointer rule:
    '   - 0..206   => use literal pointer tag
    '   - 207..462 => use compact U8 pointer form
    '   - above    => use the smallest fixed-width pointer that fits
    '
    ' Examples:
    '   index 0   => { 0x2C }
    '   index 206 => { 0xFA }
    '   index 207 => { 0xFB, 0 }
    '   index 212 => { 0xFB, 5 }
    '
    Friend Const DDATE_INLINE_B0_MIN_TAG As Byte = &H0
    Friend Const DDATE_INLINE_B0_MAX_TAG As Byte = LDATE_TICKS_B0_MAX

    Friend Const DDATE_PTR_LITERAL_BASE_TAG As Byte = &H2C
    Friend Const DDATE_PTR_LITERAL_MIN_TAG As Byte = &H2C
    Friend Const DDATE_PTR_LITERAL_MAX_TAG As Byte = &HFA
    Friend Const DDATE_PTR_LITERAL_COUNT As Integer =
        (CInt(DDATE_PTR_LITERAL_MAX_TAG) - CInt(DDATE_PTR_LITERAL_BASE_TAG) + 1)

    Friend Const DDATE_PTR_U8_TAG As Byte = &HFB
    Friend Const DDATE_PTR_U16_TAG As Byte = &HFC
    Friend Const DDATE_PTR_U24_TAG As Byte = &HFD
    Friend Const DDATE_PTR_U32_TAG As Byte = &HFE

    Friend ReadOnly DDATE_PTR_U8_CHUNK As Byte() = {DDATE_PTR_U8_TAG}
    Friend ReadOnly DDATE_PTR_U16_CHUNK As Byte() = {DDATE_PTR_U16_TAG}
    Friend ReadOnly DDATE_PTR_U24_CHUNK As Byte() = {DDATE_PTR_U24_TAG}
    Friend ReadOnly DDATE_PTR_U32_CHUNK As Byte() = {DDATE_PTR_U32_TAG}

    Friend Const DDATE_PTR_U8_BASE_INDEX As Integer = DDATE_PTR_LITERAL_COUNT
    Friend Const DDATE_PTR_U8_MAX_INDEX As Integer = DDATE_PTR_U8_BASE_INDEX + 255

    Friend Const DDATE_NULL_TAG As Byte = &HFF
    Friend ReadOnly DDATE_CHUNK_NULL As Byte() = {DDATE_NULL_TAG}
    Friend ReadOnly DDATE_CHUNK_NULL_VALUE As New PTR(DDATE_CHUNK_NULL, Array.Empty(Of Byte)())

    ' =========================================================================
    ' DDATE pointer cache
    ' =========================================================================
    '
    ' Coverage:
    '   0..462
    '
    ' Mapping:
    '   0..206   => { 0x2C + index }
    '   207..462 => { 0xFB }{ index - 207 }
    '
    Friend ReadOnly DDATE_PTR_ENCODE_0_TO_462 As PTR() =
        BuildDdatePtrEncodeCache_0_To_462()

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BuildDdatePtrEncodeCache_0_To_462() As PTR()

        Const MAX As Integer = DDATE_PTR_U8_MAX_INDEX
        Dim cache(MAX) As PTR

        For idx As Integer = 0 To MAX

            If idx < DDATE_PTR_LITERAL_COUNT Then
                Dim tag As Integer = CInt(DDATE_PTR_LITERAL_BASE_TAG) + idx
                cache(idx) = New PTR(U8_PAYLOAD_CACHE(tag), Array.Empty(Of Byte)())
            Else
                cache(idx) = New PTR(DDATE_PTR_U8_CHUNK, U8_PAYLOAD_CACHE(idx - DDATE_PTR_U8_BASE_INDEX))
            End If

        Next

        Return cache

    End Function

#End Region

#Region "Null Sentinels"

    ' =========================================================================
    ' NULL SENTINELS
    ' =========================================================================
    '
    ' Purpose:
    '   Raw fixed-size null payloads for primitive codecs that do not have a
    '   length/tag prefix of their own.
    '
    ' These values are written directly as payload bytes.
    '
    ' Float null:
    '   [0xFF][0xFF]
    '
    ' Notes:
    '   - This is now a 2-byte null sentinel shared by both Float4 and Float8.
    '   - Non-null Float4 still writes 4 bytes total.
    '   - Non-null Float8 still writes 8 bytes total.
    '   - Readers must branch after the first 2 bytes:
    '       FF FF => NULL
    '       else  => continue reading the remaining payload bytes.
    '
    Friend Const FLOAT_NULL_SENTINEL_B0 As Byte = &HFF
    Friend Const FLOAT_NULL_SENTINEL_B1 As Byte = &HFF

    Friend ReadOnly FLOAT_NULL_SENTINEL_2B As Byte() =
    {FLOAT_NULL_SENTINEL_B0, FLOAT_NULL_SENTINEL_B1}

    Friend Const BARR_NULL As Byte = 255
    Friend ReadOnly BARR_CHUNK_NULL_VALUE As New PTR({BARR_NULL}, Array.Empty(Of Byte)())

#End Region

    ''' <summary>
    ''' Represents a two-part wire chunk composed of a leading tag/length segment
    ''' and a payload segment.
    ''' </summary>
    ''' <remarks>
    ''' This shape is used heavily by cached encoders so the caller can write:
    ''' <c>ms.Write(ptr.len)</c> followed by <c>ms.Write(ptr.data)</c>.
    ''' </remarks>
    Public Structure PTR
        Public ReadOnly len As Byte()
        Public ReadOnly data As Byte()

        Public Sub New(len As Byte(), data As Byte())
            Me.len = len
            Me.data = data
        End Sub
    End Structure

    ''' <summary>
    ''' Represents a decoded or to-be-written header entry in the container header.
    ''' </summary>
    Public Structure HeaderEntry
        Public ReadOnly Key As String
        Public ReadOnly TypeCode As JSON.JsonFieldType
        Public ReadOnly Value As Object

        Public Sub New(key As String, typeCode As JSON.JsonFieldType, value As Object)
            Me.Key = key
            Me.TypeCode = typeCode
            Me.Value = value
        End Sub
    End Structure

    Public Enum CompressionMode
        Auto = 0
        None = 1
        GZip = 2
    End Enum

End Module