Imports System.IO

Namespace Encoding
    ''' <summary>
    ''' Provides the public entry point for encoding a .NET object into a Bytery container.
    ''' </summary>
    ''' <remarks>
    ''' The encoder produces a complete self-contained payload with the following raw layout:
    '''   [magic][version][zmsk][present zones...]
    '''
    ''' Canonical zone order:
    '''   [header][files][string table][date table][schema table][data]
    '''
    ''' The raw container is always assembled first. Optional outer compression is applied
    ''' only after the full Bytery payload has been finalized.
    '''
    ''' Session-scoped data resolved during encoding includes:
    '''   - schema pointers
    '''   - string pointers
    '''   - date pointers
    '''   - optional files zone entries
    ''' </remarks>
    Friend NotInheritable Class Encoder

        Private Sub New()
        End Sub

#Region "Public API"

        ''' <summary>
        ''' Encodes an object into a complete Bytery container, optionally wrapped with GZIP.
        ''' </summary>
        ''' <param name="obj">The root value to encode.</param>
        ''' <param name="headers">
        ''' Optional container header entries.
        ''' </param>
        ''' <param name="files">
        ''' Optional FILES zone entries where:
        '''   key   = file name
        '''   value = raw file bytes
        ''' </param>
        ''' <param name="compression">
        ''' The outer compression mode to apply to the final Bytery payload.
        ''' Use <see cref="CompressionMode.None"/> for raw Bytery output, or
        ''' <see cref="CompressionMode.GZip"/> to wrap the final payload with GZIP.
        ''' </param>
        ''' <returns>
        ''' A byte array containing either:
        '''   - the raw Bytery container, or
        '''   - the GZIP-compressed Bytery container.
        ''' </returns>
        ''' <remarks>
        ''' High-level pipeline:
        '''   1. Encode the root DATA zone
        '''   2. Materialize the SCHEMA TABLE discovered during the session
        '''   3. Materialize the STRING TABLE
        '''   4. Materialize the DATE TABLE
        '''   5. Write the final raw container header + ZMSK
        '''   6. Write only the zones declared by ZMSK, in canonical order
        '''   7. Optionally wrap the final container with GZIP
        '''
        ''' Raw Bytery container layout:
        '''   [4-byte magic "BYT1"]
        '''   [1-byte version]
        '''   [1-byte zmsk]
        '''   [present zones in canonical order]
        '''
        ''' Canonical zone order:
        '''   [header][files][string table][date table][schema table][data]
        '''
        ''' Notes:
        '''   - The session accumulates all per-file caches and indexes while the root
        '''     data is being written.
        '''   - The schema pass may still add strings (for example, field names), so the
        '''     final ZMSK must only be written after the schema/table discovery phase.
        '''   - GZIP is applied to the fully assembled Bytery payload, not per zone.
        ''' </remarks>
        Public Shared Function Encode(obj As Object,
                              Optional headers As Dictionary(Of String, Object) = Nothing,
                              Optional files As Dictionary(Of String, Byte()) = Nothing,
                              Optional compression As CompressionMode = CompressionMode.Auto) As Byte()

            Dim raw() As Byte = EncodeRaw(obj, headers, files)

            Select Case compression

                Case CompressionMode.None
                    Return raw

                Case CompressionMode.GZip
                    Return CompressGZip(raw)

                Case CompressionMode.Auto

                    Const AUTO_GZIP_MIN_BYTES As Integer = 1024
                    Const AUTO_GZIP_MIN_SAVINGS As Integer = 32

                    If raw Is Nothing OrElse raw.Length < AUTO_GZIP_MIN_BYTES Then
                        Return raw
                    End If

                    Dim gz() As Byte = CompressGZip(raw)

                    If gz Is Nothing Then
                        Return raw
                    End If

                    If gz.Length <= (raw.Length - AUTO_GZIP_MIN_SAVINGS) Then
                        Return gz
                    End If

                    Return raw

                Case Else
                    Throw New ArgumentOutOfRangeException(
                        NameOf(compression),
                        "Unsupported compression mode for Encode. Use None, GZip or Auto.")

            End Select

        End Function

#End Region

#Region "Raw container assembly"

        ''' <summary>
        ''' Encodes an object into the raw Bytery container format, without outer compression.
        ''' </summary>
        ''' <param name="obj">The root value to encode.</param>
        ''' <param name="headers">Optional container header entries.</param>
        ''' <param name="files">Optional FILES zone entries.</param>
        ''' <returns>A byte array containing the raw Bytery payload.</returns>
        ''' <remarks>
        ''' This method performs the complete session-driven assembly of the payload and
        ''' returns the canonical uncompressed Bytery container.
        ''' </remarks>
        Private Shared Function EncodeRaw(obj As Object,
                                  Optional headers As Dictionary(Of String, Object) = Nothing,
                                  Optional files As Dictionary(Of String, Byte()) = Nothing) As Byte()

            Dim sess As New Session(files)

            ApplyHeaders(sess, headers)

            Using dataMs As New MemoryStream()

                ' -----------------------------------------------------------------
                ' Phase 1:
                ' DATA pass discovers schemas, strings and dates needed by the file.
                ' -----------------------------------------------------------------
                sess.WriteData(obj, dataMs)

                Using schemaMs As New MemoryStream()

                    ' -----------------------------------------------------------------
                    ' Phase 2:
                    ' SCHEMA TABLE may still populate StringTable (field names, etc.).
                    ' -----------------------------------------------------------------
                    sess.WriteSchemaTable(schemaMs)

                    Using stringsMs As New MemoryStream()

                        ' -----------------------------------------------------------------
                        ' Phase 3:
                        ' STRING TABLE is only final after schema emission is complete.
                        ' -----------------------------------------------------------------
                        sess.WriteStringCacheTable(stringsMs)

                        Using datesMs As New MemoryStream()

                            ' -----------------------------------------------------------------
                            ' Phase 4:
                            ' DATE TABLE is already stable after the data pass.
                            ' -----------------------------------------------------------------
                            sess.WriteDateCacheTable(datesMs)

                            Using outMs As New MemoryStream()

                                ' -----------------------------------------------------------------
                                ' Phase 5:
                                ' Fixed container header + current ZMSK chain (v1 emits only one byte).
                                ' -----------------------------------------------------------------
                                outMs.WriteByte(FILE_MAGIC_B0)
                                outMs.WriteByte(FILE_MAGIC_B1)
                                outMs.WriteByte(FILE_MAGIC_B2)
                                outMs.WriteByte(FILE_MAGIC_B3)

                                outMs.WriteByte(FILE_VERSION_V1)

                                sess.WriteZoneMask(outMs, includeData:=True)

                                ' -----------------------------------------------------------------
                                ' Phase 6:
                                ' Write only the zones declared by ZMSK, always in canonical
                                ' protocol order.
                                ' -----------------------------------------------------------------
                                If sess.HasHeader Then
                                    sess.WriteHeader(outMs)
                                End If

                                If sess.HasFiles Then
                                    sess.WriteFilesZone(outMs)
                                End If

                                If sess.HasStringTable Then
                                    stringsMs.Position = 0
                                    stringsMs.CopyTo(outMs)
                                End If

                                If sess.HasDateTable Then
                                    datesMs.Position = 0
                                    datesMs.CopyTo(outMs)
                                End If

                                If sess.HasSchemaTable Then
                                    schemaMs.Position = 0
                                    schemaMs.CopyTo(outMs)
                                End If

                                dataMs.Position = 0
                                dataMs.CopyTo(outMs)

                                Return outMs.ToArray()

                            End Using
                        End Using
                    End Using
                End Using
            End Using

        End Function

        Private Shared Sub ApplyHeaders(sess As Session,
                                headers As Dictionary(Of String, Object))

            If sess Is Nothing Then Throw New ArgumentNullException(NameOf(sess))
            If headers Is Nothing OrElse headers.Count = 0 Then Return

            For Each kv In headers

                If String.IsNullOrWhiteSpace(kv.Key) Then
                    Throw New ArgumentException("Header key cannot be null, empty or whitespace.")
                End If

                If kv.Value Is Nothing Then
                    Throw New NotSupportedException(
                        $"Header '{kv.Key}' is Nothing. " &
                        "Dictionary(Of String,Object) cannot infer the wire type of a null header value. " &
                        "Use a non-null value or create a typed-header overload later.")
                End If

                sess.AddHeader(kv.Key, kv.Value)

            Next

        End Sub

#End Region

#Region "Compression helpers"

        ''' <summary>
        ''' Wraps a raw Bytery payload with GZIP compression.
        ''' </summary>
        ''' <param name="data">The raw Bytery container bytes.</param>
        ''' <returns>The GZIP-compressed payload.</returns>
        ''' <remarks>
        ''' The input is expected to already be a valid complete Bytery container.
        ''' This helper applies a single outer GZIP layer and does not alter the inner
        ''' Bytery layout.
        ''' </remarks>
        Private Shared Function CompressGZip(data() As Byte) As Byte()

            If data Is Nothing Then
                Return Nothing
            End If

            Using outMs As New MemoryStream()

                Using gz As New System.IO.Compression.GZipStream(
                    outMs,
                    System.IO.Compression.CompressionLevel.Fastest,
                    leaveOpen:=True)

                    gz.Write(data, 0, data.Length)
                End Using

                Return outMs.ToArray()

            End Using

        End Function

#End Region

    End Class

End Namespace