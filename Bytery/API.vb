Imports Bytery.Linq

Public Module API

    Public Function Decode(source As Byte(),
                           Optional ignoreHeader As Boolean = False,
                           Optional ByRef headerOut As List(Of HeaderEntry) = Nothing,
                           Optional ByRef filesOut As List(Of KeyValuePair(Of String, Byte())) = Nothing) As BToken

        Return Decoding.Decoder.Decode(source,
                                       ignoreHeader:=ignoreHeader,
                                       headerOut:=headerOut,
                                       filesOut:=filesOut)

    End Function

    Public Function Encode(obj As Object,
                           Optional headers As Dictionary(Of String, Object) = Nothing,
                           Optional files As Dictionary(Of String, Byte()) = Nothing,
                           Optional compression As CompressionMode = CompressionMode.Auto) As Byte()

        Return Encoding.Encoder.Encode(obj,
                                       headers:=headers,
                                       files:=files,
                                       compression:=compression)

    End Function

    Public Function Encode(token As BToken,
                           Optional headers As Dictionary(Of String, Object) = Nothing,
                           Optional files As Dictionary(Of String, Byte()) = Nothing,
                           Optional compression As CompressionMode = CompressionMode.Auto) As Byte()

        Return Encoding.EncodeBToken.Encode(token,
                                            headers:=headers,
                                            files:=files,
                                            compression:=compression)

    End Function

    Public Sub PrintBytes(bytes As Byte())
        Viewer.Render(bytes)
    End Sub

End Module