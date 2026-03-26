Imports System.Runtime.CompilerServices
Imports Bytery.JSON

Namespace Decoding

    ''' <summary>
    ''' Reads primitive values, wire chunks, and header content from a Bytery payload.
    ''' </summary>
    ''' <remarks>
    ''' This reader operates over a single in-memory byte buffer and advances an internal cursor.
    ''' It is intentionally low-level and is used by higher-level decoders such as <c>Decoder</c>
    ''' and diagnostic tools such as <c>Viewer</c>.
    ''' </remarks>
    Friend NotInheritable Class Reader

        Private _index As Integer
        Private ReadOnly _data As Byte()

#Region "Buffer bounds and cursor helpers"

        ''' <summary>
        ''' Gets the total buffer length.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function Length() As Integer
            Return _data.Length
        End Function

        ''' <summary>
        ''' Gets the number of unread bytes remaining in the buffer.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function Remaining() As Integer
            Return _data.Length - _index
        End Function

        ''' <summary>
        ''' Ensures that the requested number of bytes is available before reading.
        ''' </summary>
        ''' <param name="byteCount">The required number of bytes.</param>
        ''' <param name="context">A human-readable context string for error messages.</param>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Sub EnsureAvailable(byteCount As Integer, context As String)

            If byteCount < 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(byteCount))
            End If

            If byteCount > Me.Remaining Then
                Throw New Exception($"{context} exceeds remaining input. Need {byteCount} byte(s), remaining={Me.Remaining}.")
            End If

        End Sub

        ''' <summary>
        ''' Validates a decoded length and ensures it fits both <see cref="Int32"/> and the remaining buffer.
        ''' </summary>
        ''' <param name="length">The decoded wire length.</param>
        ''' <param name="context">A human-readable context string for error messages.</param>
        ''' <returns>The validated length as <see cref="Integer"/>.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function CheckedLength(length As Long, context As String) As Integer

            If length < 0 OrElse length > Integer.MaxValue Then
                Throw New Exception($"{context} is too large for Int32: {length}.")
            End If

            Dim n As Integer = CInt(length)

            If n > Me.Remaining Then
                Throw New Exception(
            $"{context} exceeds remaining input. Length={n}, remaining={Me.Remaining}.")
            End If

            Return n

        End Function

        ''' <summary>
        ''' Validates a decoded element count using a minimum bytes-per-item safety bound.
        ''' </summary>
        ''' <param name="count">The decoded wire count.</param>
        ''' <param name="minBytesPerItem">The minimum plausible bytes required per item.</param>
        ''' <param name="context">A human-readable context string for error messages.</param>
        ''' <returns>The validated count as <see cref="Integer"/>.</returns>
        ''' <remarks>
        ''' This method is intentionally conservative. It does not prove the full payload is valid,
        ''' but it rejects counts that are obviously impossible given the unread buffer size.
        ''' </remarks>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function CheckedCount(count As Long, minBytesPerItem As Integer, context As String) As Integer

            If count < 0 OrElse count > Integer.MaxValue Then
                Throw New Exception($"{context} is too large for Int32: {count}.")
            End If

            If minBytesPerItem <= 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(minBytesPerItem))
            End If

            Dim n As Integer = CInt(count)

            If n > 0 AndAlso n > (Me.Remaining \ minBytesPerItem) Then
                Throw New Exception(
            $"{context} is too large for remaining input. Count={n}, remaining={Me.Remaining}, minBytesPerItem={minBytesPerItem}.")
            End If

            Return n

        End Function

#End Region

        ''' <summary>
        ''' Initializes a new reader over the provided byte buffer.
        ''' </summary>
        ''' <param name="data">The source buffer. <see langword="Nothing"/> is treated as an empty array.</param>
        Public Sub New(data As Byte())
            _data = If(data, Array.Empty(Of Byte)())
            _index = 0
        End Sub

        ''' <summary>
        ''' Gets the current read cursor position.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function Position() As Integer
            Return _index
        End Function

        ''' <summary>
        ''' Returns the next byte without advancing the cursor.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function PeekByte() As Byte
            EnsureAvailable(1, "PeekByte")
            Return _data(_index)
        End Function

        ''' <summary>
        ''' Returns the after-next byte without advancing the cursor.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function PeekNextByte() As Byte
            EnsureAvailable(2, "PeekNextByte")
            Return _data(_index + 1)
        End Function

        ''' <summary>
        ''' Reads a single byte and advances the cursor by one.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadByte() As Byte
            EnsureAvailable(1, "ReadByte")
            Dim b As Byte = _data(_index)
            _index += 1
            Return b
        End Function

#Region "Big-endian unsigned payload readers"

        ''' <summary>
        ''' Reads a 2-byte unsigned big-endian integer.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt16BE() As Long
            EnsureAvailable(2, "ReadUInt16BE")
            Dim o As Integer = _index
            Dim v As Long =
                (CLng(_data(o)) << 8) Or
                 CLng(_data(o + 1))
            _index = o + 2
            Return v
        End Function

        ''' <summary>
        ''' Reads a 3-byte unsigned big-endian integer.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt24BE() As Long
            EnsureAvailable(3, "ReadUInt24BE")
            Dim o As Integer = _index
            Dim v As Long =
                (CLng(_data(o)) << 16) Or
                (CLng(_data(o + 1)) << 8) Or
                 CLng(_data(o + 2))
            _index = o + 3
            Return v
        End Function

        ''' <summary>
        ''' Reads a 4-byte unsigned big-endian integer.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt32BE() As Long
            EnsureAvailable(4, "ReadUInt32BE")
            Dim o As Integer = _index
            Dim v As Long =
                (CLng(_data(o)) << 24) Or
                (CLng(_data(o + 1)) << 16) Or
                (CLng(_data(o + 2)) << 8) Or
                 CLng(_data(o + 3))
            _index = o + 4
            Return v
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt40BE() As Long
            EnsureAvailable(5, "ReadUInt40BE")
            Dim o As Integer = _index
            Dim v As Long =
                (CLng(_data(o)) << 32) Or
                (CLng(_data(o + 1)) << 24) Or
                (CLng(_data(o + 2)) << 16) Or
                (CLng(_data(o + 3)) << 8) Or
                 CLng(_data(o + 4))
            _index = o + 5
            Return v
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt48BE() As Long
            EnsureAvailable(6, "ReadUInt48BE")
            Dim o As Integer = _index
            Dim v As Long =
                (CLng(_data(o)) << 40) Or
                (CLng(_data(o + 1)) << 32) Or
                (CLng(_data(o + 2)) << 24) Or
                (CLng(_data(o + 3)) << 16) Or
                (CLng(_data(o + 4)) << 8) Or
                 CLng(_data(o + 5))
            _index = o + 6
            Return v
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt56BE() As Long
            EnsureAvailable(7, "ReadUInt56BE")
            Dim o As Integer = _index
            Dim v As Long =
                (CLng(_data(o)) << 48) Or
                (CLng(_data(o + 1)) << 40) Or
                (CLng(_data(o + 2)) << 32) Or
                (CLng(_data(o + 3)) << 24) Or
                (CLng(_data(o + 4)) << 16) Or
                (CLng(_data(o + 5)) << 8) Or
                 CLng(_data(o + 6))
            _index = o + 7
            Return v
        End Function

        ''' <summary>
        ''' Reads an 8-byte unsigned big-endian integer.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadUInt64BE() As ULong
            EnsureAvailable(8, "ReadUInt64BE")
            Dim o As Integer = _index
            Dim v As ULong =
                (CULng(_data(o)) << 56) Or
                (CULng(_data(o + 1)) << 48) Or
                (CULng(_data(o + 2)) << 40) Or
                (CULng(_data(o + 3)) << 32) Or
                (CULng(_data(o + 4)) << 24) Or
                (CULng(_data(o + 5)) << 16) Or
                (CULng(_data(o + 6)) << 8) Or
                 CULng(_data(o + 7))
            _index = o + 8
            Return v
        End Function

#End Region

#Region "LUINT - Literal Unsigned Integer (nullable)"

        ''' <summary>
        ''' Reads an LUINT value and rejects the null marker.
        ''' </summary>
        ''' <returns>The decoded non-null LUINT value.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadLUINT() As Long
            Dim isNull As Boolean
            Dim v As Long = ReadLUINTOrNull(isNull)
            If isNull Then Throw New Exception("LUINT is NULL where a value is required.")
            Return v
        End Function

        ''' <summary>
        ''' Reads an LUINT value that may be null.
        ''' </summary>
        ''' <param name="isNull">Receives <see langword="True"/> when the null marker is encountered.</param>
        ''' <returns>The decoded value, or 0 when null is encountered.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadLUINTOrNull(ByRef isNull As Boolean) As Long
            Dim first As Byte = ReadByte()
            Return ReadLUINTFromFirst(first, isNull)
        End Function

        ''' <summary>
        ''' Decodes an LUINT value when the first tag byte has already been read.
        ''' </summary>
        ''' <param name="first">The already-consumed first tag byte.</param>
        ''' <param name="isNull">Receives <see langword="True"/> when the null marker is encountered.</param>
        ''' <returns>The decoded value, or 0 when null is encountered.</returns>
        ''' <remarks>
        ''' LUINT layout:
        '''   0..246   => literal value
        '''   247      => value = 247 + nextByte
        '''   248..254 => unsigned big-endian payload with widths 2..8 bytes
        '''   255      => NULL
        ''' </remarks>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadLUINTFromFirst(first As Byte, ByRef isNull As Boolean) As Long

            isNull = False

            Select Case first
                Case 0 To LUINT_B0_MAX : Return CLng(first)
                Case LUINT_B8 : Return CLng(LUINT_B8_BASE_VALUE + CInt(ReadByte()))
                Case LUINT_16 : Return ReadUInt16BE()
                Case LUINT_24 : Return ReadUInt24BE()
                Case LUINT_32 : Return ReadUInt32BE()
                Case LUINT_40 : Return ReadUInt40BE()
                Case LUINT_48 : Return ReadUInt48BE()
                Case LUINT_56 : Return ReadUInt56BE()
                Case LUINT_64
                    Dim u As ULong = ReadUInt64BE()
                    If u > &H7FFFFFFFFFFFFFFFUL Then Throw New Exception("LUINT too large to fit Int64.")
                    Return CLng(u)
                Case LUINT_NULL
                    isNull = True
                    Return 0L
                Case Else : Throw New Exception("Invalid LUINT First Byte: " & first)
            End Select

        End Function

#End Region

#Region "LINT - Literal Signed Integer (nullable)"

        ''' <summary>
        ''' Reads a nullable LINT value.
        ''' </summary>
        ''' <param name="isNull">Receives <see langword="True"/> when the null marker is encountered.</param>
        ''' <returns>The decoded value, or 0 when null is encountered.</returns>
        ''' <remarks>
        ''' LINT layout:
        '''   0..219     => positive literals
        '''   220..238   => negative literals -1..-19
        '''   239        => positive compact: 220 + nextByte
        '''   240..246   => positive big-endian magnitude
        '''   247        => negative compact: -(20 + nextByte)
        '''   248..254   => negative big-endian magnitude
        '''   255        => NULL
        ''' </remarks>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadLINTOrNull(ByRef isNull As Boolean) As Long

            Dim tag As Byte = ReadByte()
            isNull = False

            Select Case tag

                Case 0 To LINT_POS_LITERAL_MAX_VALUE : Return CLng(tag)
                Case LINT_NEG_LITERAL_FIRST_TAG To LINT_NEG_LITERAL_LAST_TAG : Return -CLng(CInt(tag) - LINT_POS_LITERAL_MAX_VALUE)
                Case LINT_POS_PLUS_U8_TAG : Return CLng(LINT_POS_PLUS_U8_BASE_VALUE + CInt(ReadByte()))
                Case LINT_POS_U16_TAG : Return ReadUInt16BE()
                Case LINT_POS_U24_TAG : Return ReadUInt24BE()
                Case LINT_POS_U32_TAG : Return ReadUInt32BE()
                Case LINT_POS_U40_TAG : Return ReadUInt40BE()
                Case LINT_POS_U48_TAG : Return ReadUInt48BE()
                Case LINT_POS_U56_TAG : Return ReadUInt56BE()

                Case LINT_POS_U64_TAG
                    Dim pos64 As ULong = ReadUInt64BE()
                    If pos64 > &H7FFFFFFFFFFFFFFFUL Then
                        Throw New Exception("LINT positive magnitude too large to fit Int64.")
                    End If
                    Return CLng(pos64)

                Case LINT_NEG_PLUS_U8_TAG : Return -CLng(LINT_NEG_PLUS_U8_BASE_MAG + CInt(ReadByte()))
                Case LINT_NEG_U16_TAG : Return -ReadUInt16BE()
                Case LINT_NEG_U24_TAG : Return -ReadUInt24BE()
                Case LINT_NEG_U32_TAG : Return -ReadUInt32BE()
                Case LINT_NEG_U40_TAG : Return -ReadUInt40BE()
                Case LINT_NEG_U48_TAG : Return -ReadUInt48BE()
                Case LINT_NEG_U56_TAG : Return -ReadUInt56BE()

                Case LINT_NEG_U64_TAG
                    Dim neg64 As ULong = ReadUInt64BE()

                    If neg64 = &H8000000000000000UL Then
                        Return Long.MinValue
                    End If

                    If neg64 > &H7FFFFFFFFFFFFFFFUL Then
                        Throw New Exception("LINT negative magnitude too large.")
                    End If

                    Return -CLng(neg64)

                Case LINT_NULL_TAG : isNull = True : Return 0L
                Case Else : Throw New Exception($"Invalid LINT tag: 0x{tag:X2}")

            End Select

        End Function

#End Region

#Region "Schema and pointer readers"

        ''' <summary>
        ''' Reads a schema-table pointer index using the SOBJ schema-pointer family.
        ''' </summary>
        ''' <returns>The decoded schema index.</returns>
        ''' <remarks>
        ''' Supported layouts:
        '''   252 => [252][u8]
        '''   253 => [253][u16BE]
        '''   254 => [254][u24BE]
        ''' </remarks>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadSchemaPointerIndex() As Integer

            Dim tag As Byte = ReadByte()

            Select Case tag
                Case SOBJ_SCHEMA_PTR_1B_TAG : Return CInt(ReadByte())
                Case SOBJ_SCHEMA_PTR_2B_TAG : Return CInt(ReadUInt16BE())
                Case SOBJ_SCHEMA_PTR_3B_TAG : Return CInt(ReadUInt24BE())
                Case Else : Throw New Exception($"Invalid schema pointer tag: 0x{tag:X2}")
            End Select

        End Function

#End Region

#Region "BARR - ByteArray: [LUINT length][raw bytes]"

        ''' <summary>
        ''' Reads a nullable byte array encoded as BARR.
        ''' </summary>
        ''' <param name="isNull">Receives <see langword="True"/> when the null marker is encountered.</param>
        ''' <returns>The decoded byte array, or <see langword="Nothing"/> when null is encountered.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadBarrOrNull(ByRef isNull As Boolean) As Byte()

            Dim len As Long = ReadLUINTOrNull(isNull)
            If isNull Then Return Nothing

            Return ReadBytesLiteral(CheckedLength(len, "BARR length"))

        End Function

#End Region

#Region "Fixed-size scalar readers"

        ''' <summary>
        ''' Reads a nullable BOOL value.
        ''' </summary>
        ''' <param name="isNull">Receives <see langword="True"/> when the null marker is encountered.</param>
        ''' <returns>The decoded Boolean, or <see langword="False"/> when null is encountered.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadBoolOrNull(ByRef isNull As Boolean) As Boolean

            Dim b As Byte = ReadByte()

            Select Case b
                Case BOOL_FALSE : isNull = False : Return False
                Case BOOL_TRUE : isNull = False : Return True
                Case BOOL_NULL : isNull = True : Return False
                Case Else : Throw New Exception($"Invalid BOOL payload: 0x{b:X2}")
            End Select

        End Function

        ''' <summary>
        ''' Reads a nullable 4-byte IEEE 754 floating-point payload.
        ''' </summary>
        ''' <param name="isNull">Receives <see langword="True"/> when the null sentinel is encountered.</param>
        ''' <returns>The decoded value, or 0 when null is encountered.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadSingleOrNull(ByRef isNull As Boolean) As Single

            If PeekByte() = 255 AndAlso PeekNextByte() = 255 Then
                _index += 2
                isNull = True
                Return 0.0F
            End If

            Dim bits As Long = ReadUInt32BE()

            isNull = False
            Return BitConverter.Int32BitsToSingle(UInt32LongToIntBits(bits))

        End Function

        ''' <summary>
        ''' Reinterprets an unsigned 32-bit value stored in a Long as signed 32-bit bits
        ''' without changing the bit pattern.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function UInt32LongToIntBits(u As Long) As Integer
            If u < 0 OrElse u > &HFFFFFFFFL Then Throw New Exception("UInt32 bits out of range.")
            If u <= &H7FFFFFFFL Then Return CInt(u)
            Return Integer.MinValue + CInt(u - &H80000000L)
        End Function

        ''' <summary>
        ''' Reads a nullable 8-byte IEEE 754 floating-point payload.
        ''' </summary>
        ''' <param name="isNull">Receives <see langword="True"/> when the null sentinel is encountered.</param>
        ''' <returns>The decoded value, or 0 when null is encountered.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadDoubleOrNull(ByRef isNull As Boolean) As Double

            If PeekByte() = 255 AndAlso PeekNextByte() = 255 Then
                _index += 2
                isNull = True
                Return 0.0R
            End If

            Dim bits As ULong = ReadUInt64BE()

            isNull = False
            Return BitConverter.Int64BitsToDouble(ULongToLongBits(bits))

        End Function

        ''' <summary>
        ''' Reinterprets unsigned 64-bit bits as signed 64-bit bits without changing the bit pattern.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Function ULongToLongBits(u As ULong) As Long
            If u <= &H7FFFFFFFFFFFFFFFUL Then Return CLng(u)
            Return Long.MinValue + CLng(u - &H8000000000000000UL)
        End Function

#End Region

#Region "LDATE / DDATE - Dates"

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadLDate(ByRef isNull As Boolean) As DateTime

            Dim literalUtc As DateTime
            Dim ptrIndex As Integer

            ReadDDate(literalUtc, ptrIndex, isNull)

            If ptrIndex <> -1 Then
                Throw New Exception("LDATE cannot use DDATE pointer tags.")
            End If

            Return literalUtc

        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Sub ReadDDate(ByRef literalUtc As DateTime, ByRef ptrIndex As Integer, ByRef isNull As Boolean)

            Dim tag As Byte = ReadByte()

            Select Case tag

                Case DDATE_NULL_TAG
                    isNull = True
                    literalUtc = DateTime.MinValue
                    ptrIndex = -1
                    Return

                Case 0 To LDATE_TICKS_B0_MAX

                    isNull = False
                    ptrIndex = -1

                    Dim bits As ULong =
                        (CULng(tag) << 56) Or
                        (CULng(ReadByte()) << 48) Or
                        (CULng(ReadByte()) << 40) Or
                        (CULng(ReadByte()) << 32) Or
                        (CULng(ReadByte()) << 24) Or
                        (CULng(ReadByte()) << 16) Or
                        (CULng(ReadByte()) << 8) Or
                         CULng(ReadByte())

                    If bits > &H7FFFFFFFFFFFFFFFUL Then
                        Throw New Exception("Invalid DDATE ticks encoding (negative ticks not allowed).")
                    End If

                    Dim ticks As Long = CLng(bits)

                    If ticks > DateTime.MaxValue.Ticks Then
                        Throw New Exception("Invalid DDATE ticks range.")
                    End If

                    literalUtc = New DateTime(ticks, DateTimeKind.Utc)
                    Return

                Case DDATE_PTR_LITERAL_BASE_TAG To DDATE_PTR_LITERAL_MAX_TAG
                    isNull = False
                    literalUtc = DateTime.MinValue
                    ptrIndex = CInt(tag) - CInt(DDATE_PTR_LITERAL_BASE_TAG)
                    Return

                Case DDATE_PTR_U8_TAG
                    isNull = False
                    literalUtc = DateTime.MinValue
                    ptrIndex = DDATE_PTR_U8_BASE_INDEX + CInt(ReadByte())
                    Return

                Case DDATE_PTR_U16_TAG
                    isNull = False
                    literalUtc = DateTime.MinValue
                    ptrIndex = CInt(ReadUInt16BE())
                    Return

                Case DDATE_PTR_U24_TAG
                    isNull = False
                    literalUtc = DateTime.MinValue
                    ptrIndex = CInt(ReadUInt24BE())
                    Return

                Case DDATE_PTR_U32_TAG
                    Dim u As Long = ReadUInt32BE()
                    If u > Integer.MaxValue Then Throw New Exception("DDATE pointer index too large for Int32.")
                    isNull = False
                    literalUtc = DateTime.MinValue
                    ptrIndex = CInt(u)
                    Return

                Case Else
                    Throw New Exception($"Invalid DDATE tag: 0x{tag:X2}")

            End Select

        End Sub

#End Region

#Region "Raw literal readers"

        ''' <summary>
        ''' Reads a UTF-8 literal of the specified length.
        ''' </summary>
        ''' <param name="len">The number of bytes to read.</param>
        ''' <returns>The decoded string.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadUtf8Literal(len As Integer) As String
            If len < 0 Then Throw New ArgumentOutOfRangeException(NameOf(len))
            If len = 0 Then Return String.Empty
            EnsureAvailable(len, "UTF-8 literal")
            Dim s As String = System.Text.Encoding.UTF8.GetString(_data, _index, len)
            _index += len
            Return s
        End Function

        ''' <summary>
        ''' Reads a raw byte literal of the specified length.
        ''' </summary>
        ''' <param name="len">The number of bytes to read.</param>
        ''' <returns>The copied byte array.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function ReadBytesLiteral(len As Integer) As Byte()
            If len < 0 Then Throw New ArgumentOutOfRangeException(NameOf(len))
            If len = 0 Then Return Array.Empty(Of Byte)()
            EnsureAvailable(len, "Byte literal")
            Dim out(len - 1) As Byte
            Buffer.BlockCopy(_data, _index, out, 0, len)
            _index += len
            Return out
        End Function

#End Region

#Region "LSTR - Literal String (nullable)"

        ''' <summary>
        ''' Reads a nullable LSTR value.
        ''' </summary>
        ''' <returns>The decoded string, or <see langword="Nothing"/> when null is encountered.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadLSTROrNull() As String
            Dim isNull As Boolean
            Dim l As Long = ReadLUINTOrNull(isNull)
            If isNull Then Return Nothing
            Return ReadUtf8Literal(CheckedLength(l, "LSTR length"))
        End Function

        ''' <summary>
        ''' Reads a required LSTR value and rejects the null marker.
        ''' </summary>
        ''' <returns>The decoded string.</returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function ReadLSTRRequired() As String
            Dim isNull As Boolean
            Dim l As Long = ReadLUINTOrNull(isNull)
            If isNull Then Throw New Exception("LSTR is NULL where a value is required.")
            Return ReadUtf8Literal(CheckedLength(l, "LSTR length"))
        End Function

#End Region

#Region "DSTR - Dynamic String (nullable, literal or pointer)"

        ''' <summary>
        ''' Reads a DSTR chunk and reports whether it is literal, pointer-based, or null.
        ''' </summary>
        ''' <param name="literal">Receives the literal string when present.</param>
        ''' <param name="ptrIndex">Receives the StringTable pointer index when present.</param>
        ''' <param name="isNull">Receives <see langword="True"/> when the value is null.</param>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Sub ReadDSTRChunk(ByRef literal As String, ByRef ptrIndex As Integer, ByRef isNull As Boolean)

            Dim tag As Byte = ReadByte()

            literal = Nothing
            ptrIndex = -1
            isNull = False

            Select Case tag

                Case DSTR_NULL_TAG
                    isNull = True
                    Return

                Case 0 To DSTR_LEN_LITERAL_MAX
                    literal = ReadUtf8Literal(CInt(tag))
                    Return

                Case DSTR_LEN_U8_TAG
                    literal = ReadUtf8Literal(DSTR_LEN_LITERAL_MAX + 1 + CInt(ReadByte()))
                    Return

                Case DSTR_LEN_U16_TAG
                    literal = ReadUtf8Literal(CInt(ReadUInt16BE()))
                    Return

                Case DSTR_LEN_U24_TAG
                    literal = ReadUtf8Literal(CInt(ReadUInt24BE()))
                    Return

                Case DSTR_LEN_U32_TAG
                    Dim len32 As Long = ReadUInt32BE()
                    If len32 > Integer.MaxValue Then Throw New Exception("DSTR length too large for Int32.")
                    literal = ReadUtf8Literal(CInt(len32))
                    Return

                Case DSTR_PTR_LITERAL_BASE_TAG To DSTR_PTR_LITERAL_MAX_TAG
                    ptrIndex = CInt(tag) - CInt(DSTR_PTR_LITERAL_BASE_TAG)
                    Return

                Case DSTR_PTR_U8_TAG
                    ptrIndex = DSTR_PTR_LITERAL_COUNT + CInt(ReadByte())
                    Return

                Case DSTR_PTR_U16_TAG
                    ptrIndex = CInt(ReadUInt16BE())
                    Return

                Case DSTR_PTR_U24_TAG
                    ptrIndex = CInt(ReadUInt24BE())
                    Return

                Case DSTR_PTR_U32_TAG
                    Dim ptr32 As Long = ReadUInt32BE()
                    If ptr32 > Integer.MaxValue Then Throw New Exception("DSTR pointer index too large for Int32.")
                    ptrIndex = CInt(ptr32)
                    Return

                Case Else
                    Throw New Exception($"Invalid DSTR tag: 0x{tag:X2}")

            End Select

        End Sub

#End Region

#Region "Header reader"

        Friend Sub SkipHeader()

            Dim headerBytes As Integer = CheckedLength(ReadLUINT(), "Header byteLength")
            Dim pairCount As Integer = CheckedCount(ReadLUINT(), 1, "Header pairCount")

            If pairCount = 0 Then
                If headerBytes <> 0 Then
                    Throw New Exception("Header byteLength must be 0 when pairCount = 0.")
                End If
                Return
            End If

            EnsureAvailable(headerBytes, "Header body")
            _index += headerBytes

        End Sub

        Friend Function ReadHeader() As List(Of HeaderEntry)

            Dim headerBytes As Integer = CheckedLength(ReadLUINT(), "Header byteLength")
            Dim pairCount As Integer = CheckedCount(ReadLUINT(), 1, "Header pairCount")
            Dim startPos As Integer = _index

            If pairCount = 0 Then
                If headerBytes <> 0 Then
                    Throw New Exception("Header byteLength must be 0 when pairCount = 0.")
                End If
                Return New List(Of HeaderEntry)()
            End If

            Dim list As New List(Of HeaderEntry)(pairCount)

            For i As Integer = 0 To pairCount - 1
                list.Add(ReadHeaderPair())
            Next

            Dim consumed As Integer = _index - startPos

            If consumed <> headerBytes Then
                Throw New Exception($"Header body length mismatch: consumed={consumed}, headerBytes={headerBytes}.")
            End If

            Return list

        End Function

        Friend Function ReadHeaderPair() As HeaderEntry

            Dim key As String = ReadLSTRRequired()
            Dim typeCode As JsonFieldType = CType(ReadByte(), JsonFieldType)

            If typeCode = JsonFieldType.Unknown Then
                Throw New Exception("Header value TypeCode cannot be Unknown.")
            End If

            Return New HeaderEntry(key, typeCode, ReadHeaderValue(typeCode))

        End Function

        Private Function ReadHeaderValue(typeCode As JsonFieldType) As Object
            If (typeCode And JsonFieldType.ArrayFlag) = JsonFieldType.ArrayFlag Then
                Return ReadHeaderArray(typeCode And Not JsonFieldType.ArrayFlag)
            End If
            Return ReadHeaderScalar(typeCode And Not JsonFieldType.ArrayFlag)
        End Function

        Private Function ReadHeaderScalar(baseType As JsonFieldType) As Object

            Select Case baseType

                Case JsonFieldType.Integer
                    Dim n As Boolean
                    Dim v As Long = ReadLINTOrNull(n)
                    Return If(n, Nothing, CType(v, Object))

                Case JsonFieldType.Float4Bytes
                    Dim n As Boolean
                    Dim v As Single = ReadSingleOrNull(n)
                    Return If(n, Nothing, CType(v, Object))

                Case JsonFieldType.Float8Bytes
                    Dim n As Boolean
                    Dim v As Double = ReadDoubleOrNull(n)
                    Return If(n, Nothing, CType(v, Object))

                Case JsonFieldType.Boolean
                    Dim n As Boolean
                    Dim v As Boolean = ReadBoolOrNull(n)
                    Return If(n, Nothing, CType(v, Object))

                Case JsonFieldType.Date
                    Dim n As Boolean
                    Dim v As DateTime = ReadLDate(n)
                    Return If(n, Nothing, CType(v, Object))

                Case JsonFieldType.String
                    Return ReadLSTROrNull()

                Case JsonFieldType.Bytes
                    Dim n As Boolean
                    Dim b() As Byte = ReadBarrOrNull(n)
                    Return If(n, Nothing, CType(b, Object))

                Case Else
                    Throw New Exception("Unsupported header scalar type: " & baseType.ToString())

            End Select

        End Function

        Private Function ReadHeaderArray(baseType As JsonFieldType) As Object

            Dim isNull As Boolean
            Dim countValue As Long = ReadLUINTOrNull(isNull)
            If isNull Then Return Nothing

            Select Case baseType

                Case JsonFieldType.Integer
                    Dim count As Integer = CheckedCount(countValue, 1, "Header Integer[] count")
                    If count = 0 Then Return Array.Empty(Of Long?)()

                    Dim arr(count - 1) As Long?
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Long = ReadLINTOrNull(n)
                        arr(i) = If(n, CType(Nothing, Long?), v)
                    Next
                    Return arr

                Case JsonFieldType.Float4Bytes
                    Dim count As Integer = CheckedCount(countValue, 2, "Header Float4[] count")
                    If count = 0 Then Return Array.Empty(Of Single?)()

                    Dim arr(count - 1) As Single?
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Single = ReadSingleOrNull(n)
                        arr(i) = If(n, CType(Nothing, Single?), v)
                    Next
                    Return arr

                Case JsonFieldType.Float8Bytes
                    Dim count As Integer = CheckedCount(countValue, 2, "Header Float8[] count")
                    If count = 0 Then Return Array.Empty(Of Double?)()

                    Dim arr(count - 1) As Double?
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Double = ReadDoubleOrNull(n)
                        arr(i) = If(n, CType(Nothing, Double?), v)
                    Next
                    Return arr

                Case JsonFieldType.Boolean
                    Dim count As Integer = CheckedCount(countValue, 1, "Header Boolean[] count")
                    If count = 0 Then Return Array.Empty(Of Boolean?)()

                    Dim arr(count - 1) As Boolean?
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As Boolean = ReadBoolOrNull(n)
                        arr(i) = If(n, CType(Nothing, Boolean?), v)
                    Next
                    Return arr

                Case JsonFieldType.Date
                    Dim count As Integer = CheckedCount(countValue, 1, "Header Date[] count")
                    If count = 0 Then Return Array.Empty(Of DateTime?)()

                    Dim arr(count - 1) As DateTime?
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        Dim v As DateTime = ReadLDate(n)
                        arr(i) = If(n, CType(Nothing, DateTime?), v)
                    Next
                    Return arr

                Case JsonFieldType.String
                    Dim count As Integer = CheckedCount(countValue, 1, "Header String[] count")
                    If count = 0 Then Return Array.Empty(Of String)()

                    Dim arr(count - 1) As String
                    For i As Integer = 0 To count - 1
                        arr(i) = ReadLSTROrNull()
                    Next
                    Return arr

                Case JsonFieldType.Bytes
                    Dim count As Integer = CheckedCount(countValue, 1, "Header Bytes[] count")
                    If count = 0 Then Return Array.Empty(Of Byte())()

                    Dim arr(count - 1)() As Byte
                    For i As Integer = 0 To count - 1
                        Dim n As Boolean
                        arr(i) = ReadBarrOrNull(n)
                        If n Then arr(i) = Nothing
                    Next
                    Return arr

                Case Else
                    Throw New Exception("Unsupported header array base type: " & baseType.ToString())

            End Select

        End Function

#End Region

#Region "Files reader"

        Friend Sub SkipFiles()

            Dim filesBytes As Integer = CheckedLength(ReadLUINT(), "Files body length")
            Dim fileCount As Integer = CheckedCount(ReadLUINT(), 1, "Files count")

            If fileCount = 0 Then
                If filesBytes <> 0 Then
                    Throw New Exception("FILES body length must be 0 when fileCount = 0.")
                End If
                Return
            End If

            EnsureAvailable(filesBytes, "Files body")
            _index += filesBytes

        End Sub

#End Region

    End Class

End Namespace