Namespace Benchmark.Tests

    Public Class ObscureLite
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "ObscureLite"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Basic + slightly harder cases: polymorphism, interface-typed members, heterogeneous maps, structs, long/unicode strings and non-array collections."
            End Get
        End Property

        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 1
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            Dim dog As New DogAnimal With {
                .AnimalName = "Bolt",
                .BaseCode = 100,
                .BarkLevel = 7,
                .Vaccines = New List(Of String) From {"A", "B", "cache-me"}
            }

            Dim cat As New CatAnimal With {
                .AnimalName = "Mia",
                .BaseCode = 200,
                .Lives = 9,
                .Indoor = True
            }

            Dim circle As New CircleShape With {.ShapeName = "circle-one", .Radius = 10}
            Dim rect As New RectShape With {.ShapeName = "rect-one", .Width = 7, .Height = 9}

            Dim q As New Queue(Of Integer)()
            q.Enqueue(10) : q.Enqueue(20) : q.Enqueue(30)

            Dim st As New Stack(Of String)()
            st.Push("first") : st.Push("second") : st.Push("third")

            Dim ll As New LinkedList(Of String)()
            ll.AddLast("L1") : ll.AddLast("L2") : ll.AddLast("L3")

            Dim long600 As String = New String("Z"c, 600)

            Dim zooMap As New Dictionary(Of String, AnimalBase) From {
                {"dog", dog},
                {"cat", cat},
                {"null", Nothing}
            }

            Dim heteroMap As New Dictionary(Of String, Object) From {
                {"i", 123},
                {"s", "Hello 😄 / Привет / こんにちは / عربى"},
                {"b", New Byte() {0, 1, 2, 200, 255}},
                {"arr", New Integer() {1, -2, 3, Integer.MaxValue}},
                {"list", New List(Of String) From {"x", "y", "cache-me", Nothing, "cache-me"}},
                {"obj", dog}, ' forces schema override inside map
                {"long", long600 & " / " & long600}
            }

            Return New List(Of Object) From {
                New PrimitiveSoup With {
                    .Id = 123,
                    .Name = "Hello 😄 / Привет / こんにちは / عربى",
                    .IsActive = True,
                    .ScoreSingle = 12.5F,
                    .ScoreDouble = 98765.4321R,
                    .CreatedUtc = Utc(2025, 5, 1, 10, 20, 30),
                    .OptionalInt = 42,
                    .OptionalBool = Nothing,
                    .OptionalDate = Utc(2024, 12, 31, 23, 59, 58),
                    .Kind = WeirdState.Mid,
                    .Data = New Byte() {0, 1, 2, 200, 255},
                    .EmptyData = Array.Empty(Of Byte)(),
                    .NegativeLong = -9223372036854775807L
                },
                New NestedBag With {
                    .Title = "nested-bag",
                    .Main = New ChildNode With {
                        .Code = 10,
                        .Label = "cache-me",
                        .CreatedUtc = Utc(2025, 1, 2, 3, 4, 5),
                        .Tags = New List(Of String) From {"x", "y", "cache-me", Nothing}
                    },
                    .Others = New List(Of ChildNode) From {
                        New ChildNode With {.Code = 11, .Label = "A", .CreatedUtc = Utc(2025, 2, 1, 0, 0, 0), .Tags = New List(Of String) From {"a", "b"}},
                        New ChildNode With {.Code = 12, .Label = "B", .CreatedUtc = Utc(2025, 2, 2, 0, 0, 0), .Tags = New List(Of String)()},
                        Nothing
                    }
                },
                New DictionaryMaze With {
                    .ByString = New Dictionary(Of String, Integer) From {{"one", 1}, {"two", 2}, {"neg", -7}},
                    .ByInteger = New Dictionary(Of Integer, String) From {{1, "one"}, {2, "two"}, {-3, "minus three"}},
                    .ByBoolean = New Dictionary(Of Boolean, String) From {{True, "yes"}, {False, "no"}},
                    .ByEnum = New Dictionary(Of TinyFlag, String) From {{TinyFlag.Zero, "z"}, {TinyFlag.One, "o"}, {TinyFlag.Two, "t"}},
                    .NestedList = New Dictionary(Of String, List(Of Integer)) From {{"a", New List(Of Integer) From {1, 2, 3}}, {"b", New List(Of Integer)()}, {"c", Nothing}},
                    .NestedObject = New Dictionary(Of String, ChildNode) From {{"k1", New ChildNode With {.Code = 99, .Label = "node-99", .CreatedUtc = Utc(2025, 3, 10, 8, 0, 0), .Tags = New List(Of String) From {"n1"}}}, {"k2", Nothing}}
                },
                New PolymorphContainer With {
                    .Primary = dog,
                    .Secondary = cat,
                    .Animals = New List(Of AnimalBase) From {dog, cat, Nothing},
                    .Map = New Dictionary(Of String, AnimalBase) From {{"dog", dog}, {"cat", cat}, {"none", Nothing}}
                },
                New InterfaceContainer With {
                    .MainShape = circle,
                    .OtherShape = rect,
                    .Shapes = New List(Of IShape) From {circle, rect, Nothing}
                },
                New ArrayOddities With {
                    .Ints = New Integer() {1, 2, 3, -4},
                    .NullableInts = New Integer?() {1, Nothing, 3},
                    .Names = New String() {"A", Nothing, "C"},
                    .Dates = New DateTime() {Utc(2025, 1, 1, 0, 0, 0), Utc(2025, 1, 1, 12, 30, 0)},
                    .Bools = New Boolean() {True, False, True},
                    .Doubles = New Double() {0.1R, 2.5R, -3.75R}
                },
                New StructContainer With {
                    .Point = New GeoPoint With {.Lat = -8.062R, .Lng = -34.871R, .WhenUtc = Utc(2025, 4, 1, 12, 0, 0), .Label = "Recife-ish"},
                    .PointList = New List(Of GeoPoint) From {
                        New GeoPoint With {.Lat = 0R, .Lng = 0R, .WhenUtc = Utc(2025, 4, 1, 0, 0, 0), .Label = "Zero"},
                        New GeoPoint With {.Lat = 1.23R, .Lng = 4.56R, .WhenUtc = Utc(2025, 4, 2, 0, 0, 0), .Label = "P2"}
                    },
                    .OptionalPoint = Nothing
                },
                New CollectionsZooLite With {
                    .QueueNumbers = q,
                    .StackNames = st,
                    .LinkedNames = ll,
                    .Range = New RangeEnumerable(5, 12)
                },
                New UnicodePayload With {
                    .ShortText = "çãõáéíóú ✅ 漢字",
                    .LongText = long600 & " / " & long600,
                    .RepeatedA = "cache-me",
                    .RepeatedB = "cache-me",
                    .RepeatedC = "cache-me",
                    .Lines = New List(Of String) From {"line 1", "line 2", "line 3", "cache-me"}
                },
                zooMap,
                New HeteroMapRoot With {.Map = heteroMap}
            }

        End Function

        Private Shared Function Utc(y As Integer, m As Integer, d As Integer, hh As Integer, mm As Integer, ss As Integer) As DateTime
            Return New DateTime(y, m, d, hh, mm, ss, DateTimeKind.Utc)
        End Function

#Region "Templates"

        Private Class PrimitiveSoup
            Public Property Id As Integer
            Public Property Name As String
            Public Property IsActive As Boolean
            Public Property ScoreSingle As Single
            Public Property ScoreDouble As Double
            Public Property CreatedUtc As DateTime

            Public Property OptionalInt As Integer?
            Public Property OptionalBool As Boolean?
            Public Property OptionalDate As DateTime?

            Public Property Kind As WeirdState
            Public Property Data As Byte()
            Public Property EmptyData As Byte()
            Public Property NegativeLong As Long
        End Class

        Private Enum WeirdState As Integer
            Low = 1
            Mid = 2
            High = 3
        End Enum

        Private Enum TinyFlag As Byte
            Zero = 0
            One = 1
            Two = 2
        End Enum

        Private Class ChildNode
            Public Property Code As Integer
            Public Property Label As String
            Public Property CreatedUtc As DateTime
            Public Property Tags As List(Of String)
        End Class

        Private Class NestedBag
            Public Property Title As String
            Public Property Main As ChildNode
            Public Property Others As List(Of ChildNode)
        End Class

        Private Class DictionaryMaze
            Public Property ByString As Dictionary(Of String, Integer)
            Public Property ByInteger As Dictionary(Of Integer, String)
            Public Property ByBoolean As Dictionary(Of Boolean, String)
            Public Property ByEnum As Dictionary(Of TinyFlag, String)
            Public Property NestedList As Dictionary(Of String, List(Of Integer))
            Public Property NestedObject As Dictionary(Of String, ChildNode)
        End Class

        Private MustInherit Class AnimalBase
            Public Property AnimalName As String
            Public Property BaseCode As Integer
        End Class

        Private Class DogAnimal
            Inherits AnimalBase
            Public Property BarkLevel As Integer
            Public Property Vaccines As List(Of String)
        End Class

        Private Class CatAnimal
            Inherits AnimalBase
            Public Property Lives As Integer
            Public Property Indoor As Boolean
        End Class

        Private Class PolymorphContainer
            Public Property Primary As AnimalBase
            Public Property Secondary As AnimalBase
            Public Property Animals As List(Of AnimalBase)
            Public Property Map As Dictionary(Of String, AnimalBase)
        End Class

        Private Interface IShape
            Property ShapeName As String
            ReadOnly Property Area As Double
        End Interface

        Private Class CircleShape
            Implements IShape
            Public Property ShapeName As String Implements IShape.ShapeName
            Public Property Radius As Double
            Public ReadOnly Property Area As Double Implements IShape.Area
                Get
                    Return Math.PI * Radius * Radius
                End Get
            End Property
        End Class

        Private Class RectShape
            Implements IShape
            Public Property ShapeName As String Implements IShape.ShapeName
            Public Property Width As Double
            Public Property Height As Double
            Public ReadOnly Property Area As Double Implements IShape.Area
                Get
                    Return Width * Height
                End Get
            End Property
        End Class

        Private Class InterfaceContainer
            Public Property MainShape As IShape
            Public Property OtherShape As IShape
            Public Property Shapes As List(Of IShape)
        End Class

        Private Class ArrayOddities
            Public Property Ints As Integer()
            Public Property NullableInts As Integer?()
            Public Property Names As String()
            Public Property Dates As DateTime()
            Public Property Bools As Boolean()
            Public Property Doubles As Double()
        End Class

        Private Structure GeoPoint
            Public Property Lat As Double
            Public Property Lng As Double
            Public Property WhenUtc As DateTime
            Public Property Label As String
        End Structure

        Private Class StructContainer
            Public Property Point As GeoPoint
            Public Property PointList As List(Of GeoPoint)
            Public Property OptionalPoint As GeoPoint?
        End Class

        Private Class CollectionsZooLite
            Public Property QueueNumbers As Queue(Of Integer)
            Public Property StackNames As Stack(Of String)
            Public Property LinkedNames As LinkedList(Of String)
            Public Property Range As RangeEnumerable
        End Class

        ' Custom IEnumerable(Of Integer) (array-like but not Array/List)
        Private Class RangeEnumerable
            Implements IEnumerable(Of Integer)

            Private ReadOnly _start As Integer
            Private ReadOnly _count As Integer

            Public Sub New(startValue As Integer, count As Integer)
                _start = startValue
                _count = Math.Max(0, count)
            End Sub

            Public Iterator Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
                For i As Integer = 0 To _count - 1
                    Yield _start + i
                Next
            End Function

            Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
                Return DirectCast(Me.GetEnumerator(), IEnumerator)
            End Function
        End Class

        Private Class UnicodePayload
            Public Property ShortText As String
            Public Property LongText As String
            Public Property RepeatedA As String
            Public Property RepeatedB As String
            Public Property RepeatedC As String
            Public Property Lines As List(Of String)
        End Class

        Private Class HeteroMapRoot
            Public Property Map As Dictionary(Of String, Object)
        End Class

#End Region

    End Class

End Namespace