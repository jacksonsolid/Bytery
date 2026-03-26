Option Explicit On
Option Strict On
Option Infer On

Imports System
Imports System.Collections.Generic

Namespace Benchmark.Tests

    Friend Class CyclicReferenceBoundaries
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "CyclicReferenceBoundaries"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Validates that cyclic graphs throw the expected exception instead of being encoded."
            End Get
        End Property

        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 1
            End Get
        End Property

        Public Overrides ReadOnly Property Threads As Integer
            Get
                Return 1
            End Get
        End Property

        Public Overrides ReadOnly Property IncludeNewtonsoftActors As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)
            Return New List(Of Object)()
        End Function

        Public Overrides Function BuildExpectedFailures() As List(Of ExpectedFailureCase)

            Return New List(Of ExpectedFailureCase) From {
                New ExpectedFailureCase With {
                    .Name = "A->B, B->A",
                    .Factory = AddressOf MakeSimpleObjectCycle,
                    .ExpectedExceptionType = GetType(InvalidOperationException),
                    .MessageContains = "cíclica"
                },
                New ExpectedFailureCase With {
                    .Name = "Self object",
                    .Factory = AddressOf MakeSelfObjectCycle,
                    .ExpectedExceptionType = GetType(InvalidOperationException),
                    .MessageContains = "cíclica"
                },
                New ExpectedFailureCase With {
                    .Name = "List contains itself",
                    .Factory = AddressOf MakeListSelfCycle,
                    .ExpectedExceptionType = GetType(InvalidOperationException),
                    .MessageContains = "cíclica"
                },
                New ExpectedFailureCase With {
                    .Name = "Dictionary points to parent through deep leaf",
                    .Factory = AddressOf MakeDeepDictionaryCycle,
                    .ExpectedExceptionType = GetType(InvalidOperationException),
                    .MessageContains = "cíclica"
                },
                New ExpectedFailureCase With {
                    .Name = "Array -> map -> array -> parent",
                    .Factory = AddressOf MakeArrayMapDeepCycle,
                    .ExpectedExceptionType = GetType(InvalidOperationException),
                    .MessageContains = "cíclica"
                }
            }

        End Function

#Region "Cycle factories"

        Private Shared Function MakeSimpleObjectCycle() As Object
            Dim a As New Node With {.Name = "A"}
            Dim b As New Node With {.Name = "B"}

            a.NextNode = b
            b.NextNode = a

            Return a
        End Function

        Private Shared Function MakeSelfObjectCycle() As Object
            Dim a As New Node With {.Name = "SELF"}
            a.NextNode = a
            Return a
        End Function

        Private Shared Function MakeListSelfCycle() As Object
            Dim list As New List(Of Object)
            list.Add("start")
            list.Add(list)
            Return list
        End Function

        Private Shared Function MakeDeepDictionaryCycle() As Object
            Dim root As New Dictionary(Of String, Object)(StringComparer.Ordinal)
            Dim level1 As New Dictionary(Of String, Object)(StringComparer.Ordinal)
            Dim level2 As New Dictionary(Of String, Object)(StringComparer.Ordinal)
            Dim level3 As New Dictionary(Of String, Object)(StringComparer.Ordinal)

            root("level1") = level1
            level1("level2") = level2
            level2("level3") = level3
            level3("backToRoot") = root

            Return root
        End Function

        Private Shared Function MakeArrayMapDeepCycle() As Object
            Dim root As New List(Of Object)
            Dim map As New Dictionary(Of String, Object)(StringComparer.Ordinal)
            Dim inner As New List(Of Object)

            root.Add("head")
            root.Add(map)

            map("inner") = inner
            inner.Add(123)
            inner.Add(root)

            Return root
        End Function

#End Region

#Region "Templates"

        Private NotInheritable Class Node
            Public Property Name As String
            Public Property NextNode As Node
        End Class

#End Region

    End Class

End Namespace