Imports System.Text

Module Program

    Sub Main()

        Try

            Dim headers As New Dictionary(Of String, Object) From {
                {"author", "bytery"},
                {"when", Date.UtcNow}
            }

            Dim balance As Double? = Nothing

            Dim payload As New With {
                .id = 3,
                .name = "John",
                .age = 30,
                .pet = New With {
                    .id = 1,
                    .name = "Whiskers",
                    .age = 3
                },
                .balance = balance
            }

            Dim bt As Bytery.Linq.BToken = Bytery.Linq.BToken.FromObject(payload)
            Console.WriteLine(bt.ToString)
            Dim bytes() As Byte = Bytery.Encode(bt, headers)
            Bytery.PrintBytes(bytes)
            Dim rec = Bytery.Decode(bytes)
            Console.WriteLine(rec.ToString)
            Console.ReadKey()

            Console.OutputEncoding = Encoding.UTF8

            '... testing if generated fields matches the Newtonsoft
            Benchmark.Tests.ParserTests.RunParserSuite()

            Console.WriteLine("Press any key to proceed to Size/Performance tests...")
            Console.ReadKey()

            Benchmark.Runner.RunAll()

        Catch ex As Exception
            Console.WriteLine("Error: " & ex.ToString())
        End Try

        Console.WriteLine()
        Console.WriteLine("--- END ---")
        Console.ReadKey()

    End Sub

End Module