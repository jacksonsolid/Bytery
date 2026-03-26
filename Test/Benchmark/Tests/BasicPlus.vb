Namespace Benchmark.Tests

    Public Class BasicPlus
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "BasicPlus"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Basic + Polymorphism (schema override), interface-typed fields, map with polymorphic values, structs, long strings and queue-like collections."
            End Get
        End Property

        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 100
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            Dim dog1 As New Dog With {
                .Kind = "dog",
                .Name = "rex",
                .BarkDb = 85,
                .Tag = "cache-me",
                .Toys = New List(Of String) From {"ball", "rope", "cache-me", Nothing, "cache-me"}
            }

            Dim cat1 As New Cat With {
                .Kind = "cat",
                .Name = "mimi",
                .LivesLeft = 7,
                .Tag = "cache-me",
                .Hates = New String() {"water", "vacuum", Nothing}
            }

            Dim dog2 As New Dog With {
                .Kind = "dog",
                .Name = "bolt",
                .BarkDb = 95,
                .Tag = "unique-tag-" & 123.ToString(),
                .Toys = New List(Of String) From {"stick", "bone"}
            }

            Dim polyRoot As New PolyRoot With {
                .Title = "poly-root",
                .Notes = LongText(1000),
                .CreatedUtc = Utc(2025, 3, 10, 12, 0, 0),
                .Featured = dog1, ' interface-typed -> schema override
                .Animals = New List(Of AnimalBase) From {
                    dog1,
                    cat1,
                    Nothing,
                    dog2
                },
                .ByName = New Dictionary(Of String, AnimalBase) From {
                    {"rex", dog1},
                    {"mimi", cat1},
                    {"ghost", Nothing},
                    {"bolt", dog2}
                },
                .Meta = New Dictionary(Of String, String) From {
                    {"hello", "cache-me"},
                    {"emoji", "🙂🙂🙂"},
                    {"pt", "ação"},
                    {"long", New String("x"c, 600)} ' força TagLen U16 no JSON/Bytery string length
                },
                .Numbers = BuildIntArray(1000),
                .QueueNumbers = BuildQueue(600),
                .Purchases = New List(Of Purchase) From {
                    New Purchase With {
                        .PurchaseId = 1001,
                        .Total = New Money With {.Amount = 12.5R, .Currency = "BRL"},
                        .WhenUtc = Utc(2025, 3, 10, 12, 1, 0),
                        .Status = PurchaseStatus.Paid,
                        .Notes = "cache-me"
                    },
                    New Purchase With {
                        .PurchaseId = 1002,
                        .Total = New Money With {.Amount = 0R, .Currency = "USD"},
                        .WhenUtc = Utc(2025, 3, 10, 12, 2, 0),
                        .Status = PurchaseStatus.Refunded,
                        .Notes = Nothing
                    }
                }
            }

            ' Root map (map schema como root) com valores polimórficos
            Dim zooMap As New Dictionary(Of String, AnimalBase) From {
                {"a", dog1},
                {"b", cat1},
                {"c", Nothing},
                {"d", dog2}
            }

            Return New List(Of Object) From {
                polyRoot,
                zooMap
            }

        End Function

        Private Shared Function Utc(y As Integer, m As Integer, d As Integer, hh As Integer, mm As Integer, ss As Integer) As DateTime
            Return New DateTime(y, m, d, hh, mm, ss, DateTimeKind.Utc)
        End Function

        Private Shared Function BuildIntArray(n As Integer) As Integer()
            Dim arr(Math.Max(0, n) - 1) As Integer
            For i As Integer = 0 To arr.Length - 1
                Select Case (i Mod 8)
                    Case 0 : arr(i) = 0
                    Case 1 : arr(i) = 1
                    Case 2 : arr(i) = -1
                    Case 3 : arr(i) = Integer.MaxValue
                    Case 4 : arr(i) = Integer.MinValue
                    Case 5 : arr(i) = 123456789
                    Case 6 : arr(i) = -987654321
                    Case Else : arr(i) = i
                End Select
            Next
            Return arr
        End Function

        Private Shared Function BuildQueue(n As Integer) As Queue(Of Integer)
            Dim q As New Queue(Of Integer)(capacity:=Math.Max(0, n))
            For i As Integer = 1 To n
                q.Enqueue(i Mod 97)
            Next
            Return q
        End Function

        Private Shared Function LongText(len As Integer) As String
            ' mistura ASCII + unicode/emoji para exercitar UTF-8
            Dim base As String = "Bytery cache-me ação é útil 🙂 "
            Dim sb As New System.Text.StringBuilder(len + 64)
            While sb.Length < len
                sb.Append(base)
            End While
            Return sb.ToString(0, len)
        End Function

#Region "Templates"

        ' Interface para testar schema override quando o membro é tipado como interface
        Private Interface ITagged
            Property Tag As String
        End Interface

        Private MustInherit Class AnimalBase
            Public Kind As String
            Public Name As String
        End Class

        Private Class Dog
            Inherits AnimalBase
            Implements ITagged

            Public BarkDb As Integer
            Public Toys As IList(Of String)

            Public Property Tag As String Implements ITagged.Tag
        End Class

        Private Class Cat
            Inherits AnimalBase
            Implements ITagged

            Public LivesLeft As Integer
            Public Hates As String()

            Public Property Tag As String Implements ITagged.Tag
        End Class

        Private Enum PurchaseStatus As Integer
            Pending = 0
            Paid = 1
            Refunded = 2
        End Enum

        ' Struct (TypeCode.Object) dentro do grafo
        Private Structure Money
            Public Amount As Double
            Public Currency As String
        End Structure

        Private Class Purchase
            Public PurchaseId As Integer
            Public Total As Money
            Public WhenUtc As DateTime
            Public Status As PurchaseStatus
            Public Notes As String
        End Class

        Private Class PolyRoot
            Public Title As String
            Public Notes As String
            Public CreatedUtc As DateTime

            Public Featured As ITagged ' interface-typed
            Public Animals As IList(Of AnimalBase) ' base class list (polimorfismo)
            Public ByName As IDictionary(Of String, AnimalBase) ' map com valores polimórficos

            Public Meta As IDictionary(Of String, String)

            Public Numbers As Integer()
            Public QueueNumbers As Queue(Of Integer) ' IEnumerable(Of T) (não-array)

            Public Purchases As IList(Of Purchase)
        End Class

#End Region

    End Class

End Namespace