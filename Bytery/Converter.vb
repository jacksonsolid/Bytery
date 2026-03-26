Imports System.IO
Imports System.Numerics
Imports System.Runtime.CompilerServices

''' <summary>
''' Provides allocation-friendly helpers that build reusable wire chunks for the Bytery format.
''' </summary>
''' <remarks>
''' This class only constructs encoded byte fragments. It does not write directly to streams.
''' Stream-writing helpers live in <see cref="Writer"/>.
''' </remarks>
Friend NotInheritable Class Codec

    Private Sub New()
    End Sub

#Region "LUINT - Literal Unsigned Integer"

    ''' <summary>
    ''' Encodes a non-negative integer using the LUINT wire codec.
    ''' </summary>
    ''' <param name="value">The value to encode. Must be greater than or equal to zero.</param>
    ''' <returns>
    ''' A byte array containing the complete LUINT encoding.
    ''' </returns>
    ''' <remarks>
    ''' LUINT layout:
    '''   0..246   => literal value                              (1 byte total)
    '''   247      => value = 247 + nextByte                    (2 bytes total, range 247..502)
    '''   248..254 => unsigned big-endian payload               (3..9 bytes total)
    '''   255      => NULL                                      (not used by this method)
    '''
    ''' Canonical rule:
    '''   Always use the shortest valid representation.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function LUINT(value As Long) As Byte()

        If value < 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(value), "LUINT cannot be negative.")
        End If

        Dim mag As ULong = CULng(value)

        ' Fast path:
        '   0..502 are served from the prebuilt cache.
        If mag <= CULng(LUINT_B8_MAX) Then
            Return LUINT_ENCODE_0_TO_502(CInt(mag))
        End If

        ' Fallback:
        '   Use the smallest fixed-width unsigned big-endian payload that can
        '   represent the value, from 2 to 8 bytes.
        Dim byteCount As Integer = (BitOperations.Log2(mag) >> 3) + 1
        If byteCount < 2 Then byteCount = 2
        If byteCount > 8 Then byteCount = 8

        ' LUINT_16..LUINT_64 are contiguous and map directly to payload sizes 2..8.
        Dim tag As Byte = CByte(LUINT_16 + (byteCount - 2))

        ' Output shape:
        '   [tag][payload...]
        Dim out(byteCount) As Byte
        out(0) = tag

        For i As Integer = 0 To byteCount - 1
            Dim shift As Integer = (byteCount - 1 - i) << 3
            out(1 + i) = CByte((mag >> shift) And &HFFUL)
        Next

        Return out

    End Function

#End Region

#Region "BARR - ByteArray: [LUINT length][raw bytes]"

    ''' <summary>
    ''' Encodes a byte array as a BARR wire chunk.
    ''' </summary>
    ''' <param name="data">The byte array to encode, or <see langword="Nothing"/> for null.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> contains the encoded length or null marker
    '''   - <c>data</c> contains the raw byte payload
    ''' </returns>
    ''' <remarks>
    ''' BARR layout:
    '''   [LUINT length][raw bytes]
    '''
    ''' Null layout:
    '''   [LUINT_NULL]
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function FromBytes(data() As Byte) As PTR

        If data Is Nothing Then
            Return BARR_CHUNK_NULL_VALUE
        End If

        Return New PTR(LUINT(data.Length), data)

    End Function

#End Region

#Region "LSTR/DSTR - Strings"

    ''' <summary>
    ''' Encodes a string as an LSTR chunk.
    ''' </summary>
    ''' <param name="str">The string to encode, or <see langword="Nothing"/> for null.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> is the LUINT-encoded UTF-8 length or null marker
    '''   - <c>data</c> is the UTF-8 payload
    ''' </returns>
    ''' <remarks>
    ''' LSTR layout:
    '''   [LUINT byteLengthOrNull][UTF-8 bytes]
    '''
    ''' LSTR is the literal-only string family used when no session pointer is involved.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function LSTRFromString(str As String) As PTR

        If str Is Nothing Then
            Return New PTR(U8_PAYLOAD_CACHE(LUINT_NULL), Array.Empty(Of Byte)())
        End If

        Dim utf8() As Byte = Text.Encoding.UTF8.GetBytes(str)
        Return New PTR(LUINT(utf8.Length), utf8)

    End Function

    ''' <summary>
    ''' Encodes a string as a literal DSTR chunk.
    ''' </summary>
    ''' <param name="str">The string to encode, or <see langword="Nothing"/> for null.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> contains the DSTR length tag bytes
    '''   - <c>data</c> contains the UTF-8 payload
    ''' </returns>
    ''' <remarks>
    ''' DSTR supports both literal and pointer forms.
    ''' This method always emits the literal form:
    '''   [DSTR length tag(s)][UTF-8 bytes]
    ''' </remarks>
    Public Shared Function DSTRLiteralFromString(str As String) As PTR

        If str Is Nothing Then
            Return New PTR(U8_PAYLOAD_CACHE(DSTR_NULL_TAG), Array.Empty(Of Byte)())
        End If

        Dim utf8() As Byte = Text.Encoding.UTF8.GetBytes(str)
        Dim lenBytes() As Byte = DSTRLength(utf8.Length)

        Return New PTR(lenBytes, utf8)

    End Function

    ''' <summary>
    ''' Encodes a DSTR literal length.
    ''' </summary>
    ''' <param name="byteLen">The UTF-8 byte length of the literal string.</param>
    ''' <returns>The encoded DSTR length bytes.</returns>
    ''' <remarks>
    ''' DSTR literal-length layout:
    '''   0..156   => literal length tag                         (1 byte)
    '''   247      => length = 157 + nextByte                   (2 bytes total, range 157..412)
    '''   248      => UInt16BE payload
    '''   249      => UInt24BE payload
    '''   250      => UInt32BE payload
    '''
    ''' Pointer tags and null are not produced here.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function DSTRLength(byteLen As Integer) As Byte()

        If byteLen < 0 Then Throw New ArgumentOutOfRangeException(NameOf(byteLen))

        ' Fast path:
        '   0..412 are covered by the small cache.
        If byteLen <= (DSTR_LEN_U8_BASE + 255) Then
            Return DSTR_LEN_ENCODE_0_TO_412(byteLen)
        End If

        ' Fallback:
        '   Use the smallest fixed-width big-endian payload that can represent
        '   the real byte length, from 2 to 4 bytes.
        Dim u As UInteger = CUInt(byteLen)

        Dim byteCount As Integer = (BitOperations.Log2(u) >> 3) + 1
        If byteCount < 2 Then byteCount = 2
        If byteCount > 4 Then byteCount = 4

        ' DSTR_LEN_U16..DSTR_LEN_U32 are contiguous and map to payload sizes 2..4.
        Dim tag As Byte = CByte(DSTR_LEN_U16_TAG + (byteCount - 2))

        Dim out(byteCount) As Byte
        out(0) = tag

        For i As Integer = 0 To byteCount - 1
            Dim shift As Integer = (byteCount - 1 - i) << 3
            out(1 + i) = CByte((u >> shift) And &HFFUI)
        Next

        Return out

    End Function

    ''' <summary>
    ''' Encodes a DSTR session pointer.
    ''' </summary>
    ''' <param name="index">The StringTable index to encode.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> contains the pointer tag bytes
    '''   - <c>data</c> contains any pointer payload bytes
    ''' </returns>
    ''' <remarks>
    ''' DSTR pointer layout:
    '''   157..246 => literal pointer index 0..89               (no payload)
    '''   251      => compact U8 pointer for indexes 90..345
    '''   252      => UInt16BE real index
    '''   253      => UInt24BE real index
    '''   254      => UInt32BE real index
    '''
    ''' Canonical rule:
    '''   Use the smallest valid pointer representation.
    ''' </remarks>
    Public Shared Function DSTRPointer(index As Integer) As PTR

        If index < 0 Then Throw New ArgumentOutOfRangeException(NameOf(index))

        ' Fast path:
        '   0..345 are covered by the prebuilt pointer cache.
        If index <= (DSTR_PTR_LITERAL_COUNT + 255) Then
            Return DSTR_PTR_ENCODE_0_TO_345(index)
        End If

        ' Fallback:
        '   Encode the real index using the smallest fixed-width payload.
        If index <= &HFFFF Then
            Return New PTR(U8_PAYLOAD_CACHE(DSTR_PTR_U16_TAG), New Byte() {
                CByte((index >> 8) And &HFF),
                CByte(index And &HFF)
            })
        End If

        If index <= &HFFFFFF Then
            Return New PTR(U8_PAYLOAD_CACHE(DSTR_PTR_U24_TAG), New Byte() {
                CByte((index >> 16) And &HFF),
                CByte((index >> 8) And &HFF),
                CByte(index And &HFF)
            })
        End If

        Dim u As UInteger = CUInt(index)
        Return New PTR(U8_PAYLOAD_CACHE(DSTR_PTR_U32_TAG), New Byte() {
            CByte((u >> 24) And &HFFUI),
            CByte((u >> 16) And &HFFUI),
            CByte((u >> 8) And &HFFUI),
            CByte(u And &HFFUI)
        })

    End Function

#End Region

    ''' <summary>
    ''' Returns a byte array in big-endian order.
    ''' </summary>
    ''' <param name="bytes">The source bytes.</param>
    ''' <returns>
    ''' The same array instance, reversed in place when running on little-endian systems.
    ''' </returns>
    ''' <remarks>
    ''' This helper mutates the provided array when reversal is needed.
    ''' Callers should only pass arrays they own.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function NormalizeEndian(bytes() As Byte) As Byte()
        If bytes Is Nothing Then Return Nothing
        If BitConverter.IsLittleEndian Then Array.Reverse(bytes)
        Return bytes
    End Function

#Region "LDATE/DDATE - Dates"

    ''' <summary>
    ''' Normalizes a <see cref="Date"/> to UTC semantics for the wire format.
    ''' </summary>
    ''' <param name="value">The source date value.</param>
    ''' <returns>A UTC-normalized <see cref="Date"/>.</returns>
    ''' <remarks>
    ''' Behavior by <see cref="DateTimeKind"/>:
    '''   Utc         => unchanged
    '''   Local       => converted with <c>ToUniversalTime()</c>
    '''   Unspecified => marked as UTC without shifting ticks
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function NormalizeDateUtc(value As Date) As Date

        Select Case value.Kind
            Case DateTimeKind.Utc
                Return value

            Case DateTimeKind.Local
                Return value.ToUniversalTime()

            Case Else
                Return Date.SpecifyKind(value, DateTimeKind.Utc)
        End Select

    End Function

    ''' <summary>
    ''' Encodes a date as an LDATE literal payload.
    ''' </summary>
    ''' <param name="value">The date to encode.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> is empty
    '''   - <c>data</c> contains 8 bytes of UTC ticks in big-endian order
    ''' </returns>
    ''' <remarks>
    ''' LDATE is literal-only and does not use tags or pointers.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function FromDate(value As Date) As PTR
        Dim utc As Date = NormalizeDateUtc(value)
        Return New PTR(Array.Empty(Of Byte)(), NormalizeEndian(BitConverter.GetBytes(utc.Ticks)))
    End Function

    ''' <summary>
    ''' Encodes a DDATE session pointer.
    ''' </summary>
    ''' <param name="index">The DateTable index to encode.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> contains the pointer tag bytes
    '''   - <c>data</c> contains any pointer payload bytes
    ''' </returns>
    ''' <remarks>
    ''' DDATE pointer layout:
    '''   0x2C..0xFA => literal pointer index 0..206            (no payload)
    '''   0xFB       => compact U8 pointer for indexes 207..462
    '''   0xFC       => UInt16BE real index
    '''   0xFD       => UInt24BE real index
    '''   0xFE       => UInt32BE real index
    '''
    ''' Canonical rule:
    '''   Use the smallest valid pointer representation.
    ''' </remarks>
    Public Shared Function DatePointer(index As Integer) As PTR

        If index < 0 Then Throw New ArgumentOutOfRangeException(NameOf(index))

        ' Fast path:
        '   Literal pointer tags plus the compact U8 window are prebuilt.
        If index <= DDATE_PTR_U8_MAX_INDEX Then
            Return DDATE_PTR_ENCODE_0_TO_462(index)
        End If

        ' Fallback:
        '   Encode the real index using the smallest fixed-width payload.
        If index <= &HFFFF Then
            Return New PTR(DDATE_PTR_U16_CHUNK, New Byte() {
                CByte((index >> 8) And &HFF),
                CByte(index And &HFF)
            })
        End If

        If index <= &HFFFFFF Then
            Return New PTR(DDATE_PTR_U24_CHUNK, New Byte() {
                CByte((index >> 16) And &HFF),
                CByte((index >> 8) And &HFF),
                CByte(index And &HFF)
            })
        End If

        Dim u As UInteger = CUInt(index)
        Return New PTR(DDATE_PTR_U32_CHUNK, New Byte() {
            CByte((u >> 24) And &HFFUI),
            CByte((u >> 16) And &HFFUI),
            CByte((u >> 8) And &HFFUI),
            CByte(u And &HFFUI)
        })

    End Function

#End Region

#Region "SOBJ - Schema pointer (index into schema-table, 1..3 bytes)"

    ''' <summary>
    ''' Encodes a schema-table index using the SOBJ schema-pointer family.
    ''' </summary>
    ''' <param name="index">The schema index to encode.</param>
    ''' <returns>
    ''' A <see cref="PTR"/> where:
    '''   - <c>len</c> contains the schema pointer tag
    '''   - <c>data</c> contains the pointer payload bytes
    ''' </returns>
    ''' <remarks>
    ''' Supported layouts:
    '''   252 => [252][u8]
    '''   253 => [253][u16BE]
    '''   254 => [254][u24BE]
    '''
    ''' Schema indexes larger than 0xFFFFFF are not supported by this codec.
    ''' </remarks>
    Public Shared Function SchemaPointer(index As Integer) As PTR

        If index < 0 Then Throw New ArgumentOutOfRangeException(NameOf(index), "Index cannot be negative.")

        If index <= &HFF Then
            Return SOBJ_SCHEMA_PTR_U8_ENCODE_0_TO_255(index)
        End If

        If index <= &HFFFF Then
            Return New PTR(SOBJ_SCHEMA_PTR_2B_CHUNK, New Byte() {
                CByte((index >> 8) And &HFF),
                CByte(index And &HFF)
            })
        End If

        If index <= &HFFFFFF Then
            Return New PTR(SOBJ_SCHEMA_PTR_3B_CHUNK, New Byte() {
                CByte((index >> 16) And &HFF),
                CByte((index >> 8) And &HFF),
                CByte(index And &HFF)
            })
        End If

        Throw New ArgumentOutOfRangeException(NameOf(index), "Schema index is too large (max 0xFFFFFF).")

    End Function

#End Region

End Class

''' <summary>
''' Writes primitive values and schema metadata directly to streams using Bytery wire rules.
''' </summary>
''' <remarks>
''' Unlike <see cref="Codec"/>, this class performs stream writes immediately instead of
''' returning reusable wire chunks.
''' </remarks>
Friend NotInheritable Class Writer

#Region "Low-level helpers (NO codec semantics)"

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function GetLUINTEncodedByteCount(value As Long) As Integer

        If value < 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(value))
        End If

        If value <= LUINT_B0_MAX Then Return 1
        If value <= LUINT_B8_MAX Then Return 2
        If value <= UShort.MaxValue Then Return 3
        If value <= &HFFFFFFL Then Return 4
        If value <= UInteger.MaxValue Then Return 5
        If value <= &HFFFFFFFFFFL Then Return 6
        If value <= &HFFFFFFFFFFFFL Then Return 7
        If value <= &HFFFFFFFFFFFFFFL Then Return 8

        Return 9

    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function GetLSTREncodedByteCount(value As String) As Integer

        If value Is Nothing Then
            Return 1 ' LUINT_NULL
        End If

        Dim utf8Len As Integer = Text.Encoding.UTF8.GetByteCount(value)
        Return GetLUINTEncodedByteCount(utf8Len) + utf8Len

    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function GetBarrEncodedByteCount(value As Byte()) As Integer

        If value Is Nothing Then
            Return 1 ' BARR null => 255
        End If

        Return GetLUINTEncodedByteCount(value.LongLength) + value.Length

    End Function

    ''' <summary>
    ''' Writes an unsigned integer payload using exactly the requested number of bytes in big-endian order.
    ''' </summary>
    ''' <param name="ms">The target stream.</param>
    ''' <param name="value">The unsigned value to write.</param>
    ''' <param name="byteCount">The exact number of payload bytes to emit.</param>
    ''' <remarks>
    ''' This helper is payload-only. It does not write any type tag or length prefix.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Sub WriteUIntBE(ms As Stream, value As ULong, byteCount As Integer)
        Dim shift As Integer = (byteCount - 1) << 3
        While shift >= 0
            ms.WriteByte(CByte((value >> shift) And &HFFUL))
            shift -= 8
        End While
    End Sub

    ''' <summary>
    ''' Returns a byte array in big-endian order.
    ''' </summary>
    ''' <param name="bytes">The source bytes.</param>
    ''' <returns>
    ''' The same array instance, reversed in place when running on little-endian systems.
    ''' </returns>
    ''' <remarks>
    ''' This helper mutates the provided array when reversal is needed.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function NormalizeEndian(bytes() As Byte) As Byte()
        If bytes Is Nothing Then Return Nothing
        If BitConverter.IsLittleEndian Then Array.Reverse(bytes)
        Return bytes
    End Function

#End Region

    ''' <summary>
    ''' Writes a Boolean value using the BOOL wire codec.
    ''' </summary>
    ''' <param name="o">The Boolean value, or <see langword="Nothing"/> for null.</param>
    ''' <param name="ms">The target stream.</param>
    ''' <remarks>
    ''' BOOL layout:
    '''   0 => False
    '''   1 => True
    '''   2 => NULL
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Sub WriteBool(o As Object, ms As Stream)
        If o Is Nothing Then
            ms.WriteByte(BOOL_NULL)
        ElseIf CBool(o) Then
            ms.WriteByte(BOOL_TRUE)
        Else
            ms.WriteByte(BOOL_FALSE)
        End If
    End Sub

    ''' <summary>
    ''' Writes a value using the LUINT wire codec.
    ''' </summary>
    ''' <param name="o">The value to write, or <see langword="Nothing"/> for null.</param>
    ''' <param name="ms">The target stream.</param>
    ''' <remarks>
    ''' LUINT is reserved for non-negative infrastructure integers such as counts,
    ''' lengths, and indexes.
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Sub WriteLUINT(o As Object, ms As Stream)

        If o Is Nothing Then
            ms.WriteByte(LUINT_NULL)
            Return
        End If

        Dim value As Long = CLng(o)

        ' Fast path:
        '   0..502 are handled by the prebuilt small-value cache.
        If value >= 0 AndAlso value <= LUINT_B8_MAX Then
            ms.Write(LUINT_ENCODE_0_TO_502(CInt(value)))
            Return
        End If

        If value >= 0 Then

            Dim mag As ULong = CULng(value)

            ' Fallback:
            '   Emit the shortest fixed-width unsigned big-endian representation.
            Dim count As Integer = (BitOperations.Log2(mag) >> 3) + 1

            Dim tag As Byte = CByte(LUINT_16 + (count - 2))
            ms.WriteByte(tag)

            WriteUIntBE(ms, mag, count)
            Return

        Else
            Throw New Exception("WriteLUINT: value cannot be negative: " & value)
        End If

    End Sub

    ''' <summary>
    ''' Writes a value using the LINT wire codec.
    ''' </summary>
    ''' <param name="o">The value to write, or <see langword="Nothing"/> for null.</param>
    ''' <param name="ms">The target stream.</param>
    ''' <remarks>
    ''' LINT layout:
    '''   0..219     => positive literals
    '''   220..238   => negative literals -1..-19
    '''   239        => positive compact range 220..475
    '''   240..246   => positive big-endian magnitude
    '''   247        => negative compact range -20..-275
    '''   248..254   => negative big-endian magnitude
    '''   255        => NULL
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Sub WriteLINT(o As Object, ms As Stream)

        If o Is Nothing Then
            ms.WriteByte(LINT_NULL_TAG)
            Return
        End If

        Dim value As Long = CLng(o)

        ' Fast path:
        '   Small positive and negative values are emitted from immutable caches.
        If value >= 0 AndAlso value <= LINT_POS_PLUS_U8_MAX_VALUE Then
            ms.Write(LINT_ENCODE_POS_0_TO_475(CInt(value)))
            Return
        End If

        If value >= -LINT_NEG_PLUS_U8_MAX_MAG AndAlso value <= -1 Then
            ms.Write(LINT_ENCODE_NEG_MAG_1_TO_275(CInt(-value)))
            Return
        End If

        If value >= 0 Then

            Dim mag As ULong = CULng(value)

            ' Fallback:
            '   Use the shortest positive big-endian magnitude form.
            Dim count As Integer = (BitOperations.Log2(mag) >> 3) + 1

            ms.WriteByte(CByte(LINT_POS_U16_TAG + (count - 2)))

            WriteUIntBE(ms, mag, count)
            Return

        Else

            ' Negative fallback:
            '   Use unsigned magnitude, not two's complement.
            '   Long.MinValue is encoded through magnitude 2^63.
            Dim magN As ULong =
                If(value = Long.MinValue, &H8000000000000000UL, CULng(-value))

            Dim countN As Integer = (BitOperations.Log2(magN) >> 3) + 1

            ms.WriteByte(CByte(LINT_NEG_U16_TAG + (countN - 2)))

            WriteUIntBE(ms, magN, countN)
            Return

        End If

    End Sub

#Region "SMAT - Schema Type writer (schema-table only)"

    ''' <summary>
    ''' Writes the object-form SMAT header for a schema-table entry.
    ''' </summary>
    ''' <param name="ms">The target stream.</param>
    ''' <param name="fieldCount">The number of fields in the object schema.</param>
    ''' <remarks>
    ''' Object SMAT layout:
    '''   0..229 => literal field count
    '''   230    => field count follows as LUINT
    ''' </remarks>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Sub WriteSmatObject(ms As Stream, fieldCount As Integer)

        If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
        If fieldCount < 0 Then Throw New ArgumentOutOfRangeException(NameOf(fieldCount), "fieldCount cannot be negative.")

        If fieldCount <= SMAT_OBJ_LITERAL_MAX_FIELDS Then
            ms.WriteByte(CByte(fieldCount))
            Return
        End If

        ms.WriteByte(SMAT_OBJ_LUINT_TAG)
        Writer.WriteLUINT(CLng(fieldCount), ms)

    End Sub

#End Region

    ''' <summary>
    ''' Writes a Double as an 8-byte IEEE 754 big-endian payload.
    ''' </summary>
    ''' <param name="o">The value to write, or <see langword="Nothing"/> for null.</param>
    ''' <param name="ms">The target stream.</param>
    ''' <remarks>
    ''' Null layout:
    '''   [255][255]
    '''
    ''' Non-null layout:
    '''   [8-byte IEEE 754 big-endian payload]
    '''
    ''' This method writes only the fixed-size/value payload for Float8.
    ''' It does not emit any surrounding type marker.
    ''' </remarks>
    Public Shared Sub WriteDouble(o As Object, ms As Stream)

        If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

        If o Is Nothing Then
            ms.Write(FLOAT_NULL_SENTINEL_2B)
            Return
        End If

        Dim value As Double = CDbl(o)
        Dim be() As Byte = NormalizeEndian(BitConverter.GetBytes(value))

        ' Optional but recommended:
        ' protects the reserved null sentinel from colliding with non-null payloads.
        If be(0) = FLOAT_NULL_SENTINEL_B0 AndAlso be(1) = FLOAT_NULL_SENTINEL_B1 Then
            Throw New InvalidOperationException(
            "Non-null Float8 payload starts with the reserved null sentinel [255,255].")
        End If

        ms.Write(be)

    End Sub

    ''' <summary>
    ''' Writes a Single as a 4-byte IEEE 754 big-endian payload.
    ''' </summary>
    ''' <param name="o">The value to write, or <see langword="Nothing"/> for null.</param>
    ''' <param name="ms">The target stream.</param>
    ''' <remarks>
    ''' Null layout:
    '''   [255][255]
    '''
    ''' Non-null layout:
    '''   [4-byte IEEE 754 big-endian payload]
    '''
    ''' This method writes only the fixed-size/value payload for Float4.
    ''' It does not emit any surrounding type marker.
    ''' </remarks>
    Public Shared Sub WriteSingle(o As Object, ms As Stream)

        If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

        If o Is Nothing Then
            ms.Write(FLOAT_NULL_SENTINEL_2B)
            Return
        End If

        Dim value As Single = CSng(o)
        Dim be() As Byte = NormalizeEndian(BitConverter.GetBytes(value))

        ' Optional but recommended:
        ' protects the reserved null sentinel from colliding with non-null payloads.
        If be(0) = FLOAT_NULL_SENTINEL_B0 AndAlso be(1) = FLOAT_NULL_SENTINEL_B1 Then
            Throw New InvalidOperationException(
            "Non-null Float4 payload starts with the reserved null sentinel [255,255].")
        End If

        ms.Write(be)

    End Sub

    ''' <summary>
    ''' Writes a DateTime using the DDATE inline literal form.
    ''' </summary>
    ''' <param name="o">The date value to write, or <see langword="Nothing"/> for null.</param>
    ''' <param name="ms">The target stream.</param>
    ''' <remarks>
    ''' Output:
    '''   - null => DDATE_NULL_TAG
    '''   - non-null => 8 bytes of UTC ticks in big-endian order
    '''
    ''' This method writes the inline literal form only. Session date pointers are
    ''' built through <see cref="Codec.DatePointer(Integer)"/>.
    ''' </remarks>
    Public Shared Sub WriteDate(o As Object, ms As Stream)

        If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

        If o Is Nothing Then
            ms.WriteByte(DDATE_NULL_TAG)
            Return
        End If

        Dim value As Date = Codec.NormalizeDateUtc(CDate(o))
        Dim ticks As Long = value.Ticks

        ms.Write(NormalizeEndian(BitConverter.GetBytes(ticks)))

    End Sub

End Class