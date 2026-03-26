Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.IO
Imports System.IO.Compression
Imports System.Text

Namespace Benchmark

    Public MustInherit Class TestActor

        Public Class Snapshot

            Public ActorName As String

            Public Iterations As Integer
            Public EncodeOps As Integer
            Public DecodeOps As Integer
            Public EncodeBytesLength As Long
            Public TotalEncodeTicks As Long
            Public TotalDecodeTicks As Long

            Public Sub New(a As TestActor)
                Me.ActorName = a.Name
                Me.Iterations = a.Iterations
                Me.EncodeOps = a.EncodeOps
                Me.DecodeOps = a.DecodeOps
                Me.EncodeBytesLength = a.EncodeBytesLength
                Me.TotalEncodeTicks = a.TotalEncodeTicks
                Me.TotalDecodeTicks = a.TotalDecodeTicks
            End Sub

            Public Sub Sum(s As Snapshot)
                Me.Iterations += s.Iterations
                Me.EncodeOps += s.EncodeOps
                Me.DecodeOps += s.DecodeOps
                Me.EncodeBytesLength += s.EncodeBytesLength
                Me.TotalEncodeTicks += s.TotalEncodeTicks
                Me.TotalDecodeTicks += s.TotalDecodeTicks
            End Sub

        End Class

        Public MustOverride ReadOnly Property Name As String

        Public Iterations As Integer
        Public EncodeOps As Integer
        Public DecodeOps As Integer

        Public EncodeBytesLength As Long
        Public TotalEncodeTicks As Long
        Public TotalDecodeTicks As Long

        ' Dont counts, just warms up the JIT/caches
        Public Sub Warmup(obs As ICollection(Of Object))
            ' EncodedBytesLength doesnot change while testing
            For Each o In obs
                Try
                    Me.EncodeBytesLength += Me.EncodeObject(o)
                Catch ex As Exception
                    Console.WriteLine($"Warmup-{Me.Name} falhou com: {o} {JToken.FromObject(o).ToString}: " & ex.Message)
                End Try
            Next
            DecodeData()
        End Sub

        Public Function ToSnapshot() As Snapshot
            Return New Snapshot(Me)
        End Function

        Public Sub Run(iterations As Integer, rootObject As Object)

            Me.Iterations = iterations

            Dim sw As New Stopwatch()

            For k As Integer = 1 To iterations

                Try
                    sw.Restart()
                    Dim len As Integer = EncodeObject(rootObject)
                    sw.Stop()
                Catch ex As Exception
                    Console.WriteLine($"Encode-{Me.Name} falhou com: {rootObject} {JToken.FromObject(rootObject).ToString}: " & ex.Message)
                End Try

                TotalEncodeTicks += sw.ElapsedTicks
                EncodeOps += 1

                Try
                    sw.Restart()
                    DecodeData()
                    sw.Stop()
                Catch ex As Exception
                    Console.WriteLine($"Decode-{Me.Name} falhou com: {rootObject} {JToken.FromObject(rootObject).ToString}: " & ex.Message)
                End Try

                TotalDecodeTicks += sw.ElapsedTicks
                DecodeOps += 1

            Next

        End Sub

        Public ReadOnly Property EncodeMs As Double
            Get
                Return (TotalEncodeTicks * 1000.0R) / Stopwatch.Frequency
            End Get
        End Property

        Public ReadOnly Property DecodeMs As Double
            Get
                Return (TotalDecodeTicks * 1000.0R) / Stopwatch.Frequency
            End Get
        End Property

        Public ReadOnly Property TotalMs As Double
            Get
                Return EncodeMs + DecodeMs
            End Get
        End Property

        Public ReadOnly Property AvgEncodeMsPerOp As Double
            Get
                If EncodeOps <= 0 Then Return 0
                Return EncodeMs / EncodeOps
            End Get
        End Property

        Public ReadOnly Property AvgDecodeMsPerOp As Double
            Get
                If DecodeOps <= 0 Then Return 0
                Return DecodeMs / DecodeOps
            End Get
        End Property

        Public MustOverride Function EncodeObject(rootObject As Object) As Integer
        Public MustOverride Sub DecodeData()

        ' -----------------------------
        ' Newtonsoft
        ' -----------------------------
        Public Class ActorNewtonsoft
            Inherits TestActor

            Private Class ByteArrayAsNumberArrayConverter
                Inherits JsonConverter

                Public Overrides Function CanConvert(objectType As Type) As Boolean
                    Return objectType Is GetType(Byte())
                End Function

                Public Overrides Sub WriteJson(writer As JsonWriter, value As Object, serializer As JsonSerializer)
                    Dim bytes = DirectCast(value, Byte())
                    writer.WriteStartArray()
                    For Each b In bytes
                        writer.WriteValue(b)
                    Next
                    writer.WriteEndArray()
                End Sub

                Public Overrides Function ReadJson(reader As JsonReader,
                                               objectType As Type,
                                               existingValue As Object,
                                               serializer As JsonSerializer) As Object
                    Dim arr = JArray.Load(reader)
                    Return arr.Select(Function(t) CByte(t)).ToArray()
                End Function
            End Class

            Private _data As String

            Private Shared ReadOnly _serializer As JsonSerializer = CreateSerializer()

            Private Shared Function CreateSerializer() As JsonSerializer
                Dim settings As New JsonSerializerSettings()
                settings.Converters.Add(New ByteArrayAsNumberArrayConverter())
                Return JsonSerializer.Create(settings)
            End Function

            Public Overrides ReadOnly Property Name As String
                Get
                    Return "Newtonsoft"
                End Get
            End Property

            Public Overrides Sub DecodeData()
                JToken.Parse(Me._data)
            End Sub

            Public Overrides Function EncodeObject(rootObject As Object) As Integer
                ' IMPORTANT: Formatting.None para não "inflar" JSON com indentação
                Me._data = JToken.FromObject(rootObject, _serializer).ToString(Formatting.None)
                Return Me._data.Length
            End Function

        End Class

        Public Class ActorBytery
            Inherits TestActor

            Private _data() As Byte

            Public Overrides ReadOnly Property Name As String
                Get
                    Return "Bytery"
                End Get
            End Property

            Public Overrides Sub DecodeData()
                Bytery.Decode(Me._data)
            End Sub

            Public Overrides Function EncodeObject(rootObject As Object) As Integer
                Me._data = Bytery.Encode(rootObject)
                Return Me._data.Length
            End Function

        End Class

        Public Class ActorByteryVV
            Inherits TestActor

            Private _data() As Byte

            Public Overrides ReadOnly Property Name As String
                Get
                    Return "Bytery VV"
                End Get
            End Property

            Public Overrides Sub DecodeData()
                Bytery.Decode(Me._data)
            End Sub

            Public Overrides Function EncodeObject(rootObject As Object) As Integer
                Me._data = Bytery.Encode(Bytery.Decode(Bytery.Encode(rootObject)))
                Return Me._data.Length
            End Function

        End Class

        Public Class ActorNewtonsoftGZip
            Inherits TestActor

            Private Class ByteArrayAsNumberArrayConverter
                Inherits JsonConverter

                Public Overrides Function CanConvert(objectType As Type) As Boolean
                    Return objectType Is GetType(Byte())
                End Function

                Public Overrides Sub WriteJson(writer As JsonWriter, value As Object, serializer As JsonSerializer)
                    Dim bytes = DirectCast(value, Byte())
                    writer.WriteStartArray()
                    For Each b In bytes
                        writer.WriteValue(b)
                    Next
                    writer.WriteEndArray()
                End Sub

                Public Overrides Function ReadJson(reader As JsonReader,
                                                   objectType As Type,
                                                   existingValue As Object,
                                                   serializer As JsonSerializer) As Object
                    Dim arr = JArray.Load(reader)
                    Return arr.Select(Function(t) CByte(t)).ToArray()
                End Function
            End Class

            Private _data() As Byte

            Private Shared ReadOnly _serializer As JsonSerializer = CreateSerializer()

            Private Shared Function CreateSerializer() As JsonSerializer
                Dim settings As New JsonSerializerSettings()
                settings.Converters.Add(New ByteArrayAsNumberArrayConverter())
                Return JsonSerializer.Create(settings)
            End Function

            Public Overrides ReadOnly Property Name As String
                Get
                    Return "Newtonsoft + GZIP"
                End Get
            End Property

            Public Overrides Sub DecodeData()

                Using input As New MemoryStream(Me._data)
                    Using gzip As New GZipStream(input, CompressionMode.Decompress, leaveOpen:=False)
                        Using output As New MemoryStream()
                            gzip.CopyTo(output)
                            Dim json As String = Encoding.UTF8.GetString(output.ToArray())
                            JToken.Parse(json)
                        End Using
                    End Using
                End Using

            End Sub

            Public Overrides Function EncodeObject(rootObject As Object) As Integer

                Dim json As String = JToken.FromObject(rootObject, _serializer).ToString(Formatting.None)
                Dim utf8() As Byte = Encoding.UTF8.GetBytes(json)

                Using output As New MemoryStream()
                    Using gzip As New GZipStream(output, CompressionLevel.Fastest, leaveOpen:=True)
                        gzip.Write(utf8, 0, utf8.Length)
                    End Using

                    Me._data = output.ToArray()
                End Using

                Return Me._data.Length

            End Function

        End Class
        Public Class ActorByteryRaw
            Inherits TestActor

            Private _data() As Byte

            Public Overrides ReadOnly Property Name As String
                Get
                    Return "Bytery w/o GZIP"
                End Get
            End Property

            Public Overrides Sub DecodeData()
                Bytery.Decode(_data)
            End Sub

            Public Overrides Function EncodeObject(rootObject As Object) As Integer
                Me._data = Bytery.Encode(rootObject, compression:=Bytery.Constants.CompressionMode.None)
                Return Me._data.Length
            End Function

        End Class
    End Class

End Namespace