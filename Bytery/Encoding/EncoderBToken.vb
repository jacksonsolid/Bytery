Imports System.IO
Imports Bytery.Linq

Namespace Encoding

    Friend Class EncodeBToken

        Public Shared Function Encode(token As BToken,
                                      Optional headers As Dictionary(Of String, Object) = Nothing,
                                      Optional files As Dictionary(Of String, Byte()) = Nothing,
                                      Optional compression As CompressionMode = CompressionMode.Auto) As Byte()

            Dim s As New SessionBToken(files)
            ApplyHeaders(s, headers)

            Using dataMs As New MemoryStream()

                s.WriteData(token, dataMs)

                Using schemaMs As New MemoryStream()

                    s.WriteSchemaTable(schemaMs)

                    Using stringsMs As New MemoryStream()

                        s.WriteStringCacheTable(stringsMs)

                        Using datesMs As New MemoryStream()

                            s.WriteDateCacheTable(datesMs)

                            Using raw As New MemoryStream()

                                raw.WriteByte(FILE_MAGIC_B0)
                                raw.WriteByte(FILE_MAGIC_B1)
                                raw.WriteByte(FILE_MAGIC_B2)
                                raw.WriteByte(FILE_MAGIC_B3)
                                raw.WriteByte(FILE_VERSION_V1)

                                s.WriteZoneMask(raw, includeData:=True)

                                If s.HasHeader Then
                                    s.WriteHeader(raw)
                                End If

                                If s.HasFiles Then
                                    s.WriteFilesZone(raw)
                                End If

                                If s.HasStringTable Then
                                    stringsMs.Position = 0
                                    stringsMs.CopyTo(raw)
                                End If

                                If s.HasDateTable Then
                                    datesMs.Position = 0
                                    datesMs.CopyTo(raw)
                                End If

                                If s.HasSchemaTable Then
                                    schemaMs.Position = 0
                                    schemaMs.CopyTo(raw)
                                End If

                                dataMs.Position = 0
                                dataMs.CopyTo(raw)

                                Dim bytes() As Byte = raw.ToArray()

                                Select Case compression

                                    Case CompressionMode.None
                                        Return bytes

                                    Case CompressionMode.GZip
                                        Return CompressGZip(bytes)

                                    Case CompressionMode.Auto

                                        Const AUTO_GZIP_MIN_BYTES As Integer = 1024
                                        Const AUTO_GZIP_MIN_SAVINGS As Integer = 32

                                        If bytes Is Nothing OrElse bytes.Length < AUTO_GZIP_MIN_BYTES Then
                                            Return bytes
                                        End If

                                        Dim gz() As Byte = CompressGZip(bytes)

                                        If gz Is Nothing Then
                                            Return bytes
                                        End If

                                        If gz.Length <= (bytes.Length - AUTO_GZIP_MIN_SAVINGS) Then
                                            Return gz
                                        End If

                                        Return bytes

                                    Case Else
                                        Throw New ArgumentOutOfRangeException(
                                            NameOf(compression),
                                            "Unsupported compression mode for Encode. Use None, GZip or Auto.")

                                End Select

                            End Using
                        End Using
                    End Using
                End Using
            End Using

        End Function

        Private Shared Function CompressGZip(data As Byte()) As Byte()
            Using output As New MemoryStream()
                Using gz As New IO.Compression.GZipStream(output, IO.Compression.CompressionMode.Compress, leaveOpen:=True)
                    gz.Write(data, 0, data.Length)
                End Using
                Return output.ToArray()
            End Using
        End Function

        Private Shared Sub ApplyHeaders(sess As SessionBToken,
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

    End Class

End Namespace