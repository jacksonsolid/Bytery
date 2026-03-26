Namespace Benchmark

    Public Class Runner

        Public Shared Sub RunAll()

            Dim tests() As Tests.Test = {
                New Tests.Basic,
                New Tests.BasicPlus,
                New Tests.ObscureLite,
                New Tests.ObscureHeavy,
                New Tests.WireFormatBoundaries,
                New Tests.CyclicReferenceBoundaries,
                New Tests.NormalRegular,
                New Tests.NormalHeavy
            }

            For Each t As Tests.Test In tests
                ConsoleReporter.PrintSummary(t.Run())
            Next

        End Sub

    End Class

End Namespace