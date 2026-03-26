Namespace Benchmark.Tests

    Public Class Basic
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "Basic"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Tests Basic operations, like handling null values, Min/Max values, some edge-cases etc."
            End Get
        End Property

        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 100
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            Return New List(Of Object) From {
                 New BasicPrimitives With {
                    .Id = 123,
                    .Name = "hello",
                    .IsActive = True,
                    .ScoreSingle = 12.5F,
                    .ScoreDouble = -3.75R,
                    .CreatedUtc = Utc(2025, 1, 2, 3, 4, 5),
                    .Payload = New Integer() {0, 1, 2, 200, 255},
                    .EmptyPayload = Array.Empty(Of Integer)()
                },
                New BasicNullables With {
                    .OptionalInt = 42,
                    .OptionalInt2 = Nothing,
                    .OptionalBool = True,
                    .OptionalBool2 = Nothing,
                    .OptionalDate = Utc(2025, 2, 1, 0, 0, 0),
                    .OptionalDate2 = Nothing
                },
                New BasicArrays With {
                    .Ints = New Integer() {1, 2, 3, -4},
                    .NullableInts = New Integer?() {1, Nothing, 3},
                    .Bools = New Boolean() {True, False, True},
                    .Doubles = New Double() {0.1, 2.5, -3.75},
                    .Dates = New DateTime() {Utc(2025, 1, 1, 0, 0, 0), Utc(2025, 1, 1, 12, 30, 0)},
                    .Names = New String() {"A", Nothing, "C"}
                },
                New BasicParent With {
                    .Title = "parent",
                    .Child = New BasicChild With {.Code = 10, .Label = "child"},
                    .Children = New List(Of BasicChild) From {
                        New BasicChild With {.Code = 11, .Label = "A"},
                        New BasicChild With {.Code = 12, .Label = "B"},
                        Nothing
                    }
                },
                New BasicDictionaryHolder With {
                    .ByStringInt = New Dictionary(Of String, Integer) From {{"one", 1}, {"two", 2}, {"neg", -7}},
                    .ByStringString = New Dictionary(Of String, String) From {{"a", "A"}, {"b", "B"}, {"c", Nothing}}
                },
                New BasicRepeatedStrings With {
                    .A = "cache-me",
                    .B = "cache-me",
                    .C = "cache-me",
                    .List = New List(Of String) From {"x", "y", "cache-me", Nothing, "cache-me"}
                },
                BasicFactories.MakeClientBatch(1000)
            }

        End Function

        Private Shared Function Utc(y As Integer, m As Integer, d As Integer, hh As Integer, mm As Integer, ss As Integer) As DateTime
            Return New DateTime(y, m, d, hh, mm, ss, DateTimeKind.Utc)
        End Function

#Region "Templates"

        Private Class BasicPrimitives
            Public Id As Integer
            Public Name As String
            Public IsActive As Boolean
            Public ScoreSingle As Single
            Public ScoreDouble As Double
            Public CreatedUtc As Date
            Public Payload As Integer()
            Public EmptyPayload As Integer()
        End Class

        Private Class BasicNullables
                Public OptionalInt As Integer?
                Public OptionalInt2 As Integer?
                Public OptionalBool As Boolean?
                Public OptionalBool2 As Boolean?
                Public OptionalDate As DateTime?
                Public OptionalDate2 As DateTime?
            End Class

            Private Class BasicArrays
                Public Ints As Integer()
                Public NullableInts As Integer?()
                Public Bools As Boolean()
                Public Doubles As Double()
                Public Dates As DateTime()
                Public Names As String()
            End Class

            Private Class BasicChild
                Public Code As Integer
                Public Label As String
            End Class

            Private Class BasicParent
                Public Title As String
                Public Child As BasicChild
                Public Children As IList(Of BasicChild)
            End Class

            Private Class BasicDictionaryHolder
                Public ByStringInt As IDictionary(Of String, Integer)
                Public ByStringString As IDictionary(Of String, String)
            End Class

            Private Class BasicRepeatedStrings
                Public A As String
                Public B As String
                Public C As String
                Public List As IList(Of String)
            End Class

            Public Enum BasicPurchaseStatus As Integer
                Pending = 0
                Paid = 1
                Refunded = 2
            End Enum

            Private Class BasicPurchase
                Public PurchaseId As Integer
                Public Amount As Double
                Public WhenUtc As DateTime
                Public Status As BasicPurchaseStatus
            End Class

            Private Class BasicClient
                Public ClientId As Integer
                Public ClientName As String
                Public IsVip As Boolean
                Public CreatedUtc As DateTime
                Public Tags As IList(Of String)
                Public Purchases As IList(Of BasicPurchase)
            End Class

            Private Class BasicBatchRoot
                Public Title As String
                Public Clients As IList(Of BasicClient)
            End Class

            Private NotInheritable Class BasicFactories

                Private Sub New()
                End Sub

                Public Shared Function MakeSmallClient(id As Integer) As BasicClient

                    Dim baseDate As DateTime = New DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(id Mod 30)
                    Dim vip As Boolean = ((id And 1) = 0)

                    Dim purchases As New List(Of BasicPurchase)(capacity:=3)
                    For i As Integer = 0 To 2
                        Dim status As BasicPurchaseStatus = CType((id + i) Mod 3, BasicPurchaseStatus)
                        purchases.Add(New BasicPurchase With {
                    .PurchaseId = id * 10 + i,
                    .Amount = (id * 0.1) + i,
                    .WhenUtc = baseDate.AddMinutes(i),
                    .Status = status
                })
                    Next

                    Dim tags As New List(Of String) From {
                "basic",
                "basic",
                If(vip, "vip", "regular")
            }

                    Return New BasicClient With {
                .ClientId = id,
                .ClientName = "client-" & id.ToString(),
                .IsVip = vip,
                .CreatedUtc = baseDate,
                .Tags = tags,
                .Purchases = purchases
            }

                End Function

                Public Shared Function MakeClientBatch(count As Integer) As BasicBatchRoot

                    Dim list As New List(Of BasicClient)(capacity:=Math.Max(0, count))
                    For i As Integer = 1 To count
                        list.Add(MakeSmallClient(i))
                    Next

                    Return New BasicBatchRoot With {
                    .Title = "batch",
                    .Clients = list
                }

                End Function

            End Class

#End Region

        End Class

End Namespace