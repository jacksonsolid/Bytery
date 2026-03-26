Namespace Benchmark

    Public NotInheritable Class Summary

        Public TestName As String
        Public TestDescription As String
        Public TestIterations As Integer
        Public ThreadsUsed As Integer

        Public Snapshots As New Dictionary(Of String, TestActor.Snapshot)

        Public Sub New(t As Tests.Test)
            Me.TestName = t.Name
            Me.TestDescription = t.Description
            Me.TestIterations = t.Iterations
        End Sub

    End Class

End Namespace
