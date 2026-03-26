Option Explicit On
Option Strict On
Option Infer On

Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Dynamic
Imports System.Text

Namespace Benchmark.Tests

    Public Class ObscureHeavy
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "ObscureHeavy"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Ultra-hard cases: deep heterogeneous maps, nested object graphs, schema overrides everywhere, huge mixed collections, Unicode storms, and tricky null placements."
            End Get
        End Property

        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 10
            End Get
        End Property

        Public Overrides ReadOnly Property Threads As Integer
            Get
                Return Environment.ProcessorCount
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            Dim long2k As String = New String("Ω"c, 2048)
            Dim long8k As String = New String("猫"c, 8192)

            Dim repeated As String = "cache-me-🔥-重复-キャッシュ-"
            Dim repeated2 As String = "cache-me-🔥-重复-キャッシュ-" ' mesma string (internamente igual) pra estressar o string-table

            ' Polimorfismo (base -> derived) + overrides dentro de map
            Dim dog As New DogAnimal With {
                .AnimalName = "Bolt",
                .BaseCode = 100,
                .BarkLevel = 9,
                .Vaccines = New List(Of String) From {repeated, "A", "B", Nothing, repeated, repeated2}
            }

            Dim cat As New CatAnimal With {
                .AnimalName = "Mia",
                .BaseCode = 200,
                .Lives = 8,
                .Indoor = True
            }

            Dim snake As New SnakeAnimal With {
                .AnimalName = "Sly",
                .BaseCode = 300,
                .Venom = True,
                .LengthCm = 175
            }

            Dim circle As New CircleShape With {.ShapeName = "C1", .Radius = 123.456R}
            Dim rect As New RectShape With {.ShapeName = "R1", .Width = 7.5R, .Height = 9.25R}

            ' ExpandoObject (vira IDictionary) => map heterogêneo com valores ruins
            Dim exp As New ExpandoObject()
            Dim expDict As IDictionary(Of String, Object) = DirectCast(exp, IDictionary(Of String, Object))
            expDict("kind") = "expando"
            expDict("n") = 123
            expDict("b") = New Byte() {0, 1, 2, 3, 255}
            expDict("u") = "😄 Привет こんにちは عربى 漢字"
            expDict("obj") = dog ' schema override dentro de map
            expDict("arr") = New Object() {1, "x", Nothing, cat, New Integer() {1, 2, -3}, New List(Of String) From {repeated, Nothing, repeated}}

            ' Mistura profunda: Dictionary(Of String,Object) contendo outros Dictionary(Of String,Object) e listas
            Dim deep As Dictionary(Of String, Object) = BuildDeepChaosGraph(
                dog:=dog,
                cat:=cat,
                snake:=snake,
                circle:=circle,
                rect:=rect,
                repeated:=repeated,
                long2k:=long2k,
                long8k:=long8k,
                expando:=exp
            )

            ' Coleções “não-array”: Queue/Stack/LinkedList cheias de coisas
            Dim q As New Queue(Of Object)()
            q.Enqueue(1)
            q.Enqueue("two")
            q.Enqueue(Nothing)
            q.Enqueue(dog)
            q.Enqueue(New Integer() {Integer.MinValue, -1, 0, 1, Integer.MaxValue})

            Dim st As New Stack(Of Object)()
            st.Push("first")
            st.Push(cat)
            st.Push(New Byte() {10, 20, 30})
            st.Push(Nothing)
            st.Push("last-" & repeated)

            Dim ll As New LinkedList(Of Object)()
            ll.AddLast("L1")
            ll.AddLast(exp)
            ll.AddLast(New List(Of Object) From {snake, Nothing, "x", repeated})

            ' Listas heterogêneas grandes (para estressar schema override e array-encode)
            Dim bigMixed As New List(Of Object)(capacity:=2000)
            For i As Integer = 0 To 399
                bigMixed.Add(i)
                bigMixed.Add("s-" & repeated)
                bigMixed.Add(If(i Mod 7 = 0, Nothing, Utc(2025, 1, 1, 0, 0, 0).AddSeconds(i)))
                bigMixed.Add(If(i Mod 11 = 0, dog, If(i Mod 13 = 0, CObj(cat), CObj(snake)))) ' muitos overrides
                bigMixed.Add(New Byte() {CByte(i And &HFF), 1, 2, 3, 4})
            Next

            ' Map heterogêneo raiz (o “terror”)
            Dim rootHetero As New Dictionary(Of String, Object) From {
                {"title", "ObscureHeavy-" & repeated},
                {"unicode", "Hello 😄 / Привет / こんにちは / عربى / 漢字 / 한글"},
                {"long2k", long2k},
                {"long8k", long8k},
                {"nullTop", Nothing},
                {"pi", Math.PI},
                {"minInt", Integer.MinValue},
                {"maxInt", Integer.MaxValue},
                {"minLong", Long.MinValue + 1},
                {"maxLong", Long.MaxValue},
                {"bytesA", New Byte() {0, 1, 2, 200, 255}},
                {"bytesEmpty", Array.Empty(Of Byte)()},
                {"date", Utc(2025, 12, 31, 23, 59, 59)},
                {"dateNull", CType(Nothing, DateTime?)},
                {"animalsBaseMap", New Dictionary(Of String, AnimalBase) From {
                    {"dog", dog},
                    {"cat", cat},
                    {"snake", snake},
                    {"none", Nothing}
                }},
                {"shapesIfaceList", New List(Of IShape) From {circle, rect, Nothing}},
                {"expando", exp}, ' IDictionary
                {"queueMixed", q},
                {"stackMixed", st},
                {"linkedMixed", ll},
                {"bigMixed", bigMixed},
                {"deep", deep}
            }

            ' Objetos “fortes” (classes) para o lado DotNetSchema também apanhar
            Dim container As New HeavyContainer With {
                .Id = GuidLikeId(32),
                .Name = "heavy-" & repeated,
                .CreatedUtc = Utc(2025, 6, 1, 12, 34, 56),
                .Primary = dog,
                .Secondary = cat,
                .AllAnimals = New List(Of AnimalBase) From {dog, cat, snake, Nothing},
                .AnyMap = rootHetero,
                .AnyList = bigMixed,
                .AnyQueue = q,
                .AnyStack = st,
                .AnyLinked = ll,
                .Flags = New Dictionary(Of String, Boolean?) From {{"a", True}, {"b", Nothing}, {"c", False}},
                .Numbers = New Integer?() {1, Nothing, -2, 0, Integer.MaxValue, Integer.MinValue},
                .Doubles = New Double?() {0.1R, Nothing, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity, -99999.12345R},
                .Payloads = New List(Of Byte()) From {New Byte() {1, 2, 3}, Nothing, Array.Empty(Of Byte)(), New Byte() {255, 254, 253}}
            }

            ' Retorna múltiplas raízes pra variar cenários do encoder
            Return New List(Of Object) From {
                rootHetero, ' root map heterogêneo
                container,  ' root objeto forte com muitos campos
                deep,       ' root deep graph
                exp         ' root IDictionary via expando
            }

        End Function

        ' -------------------------------
        ' Deep chaos graph builder
        ' -------------------------------
        Private Shared Function BuildDeepChaosGraph(dog As DogAnimal,
                                                    cat As CatAnimal,
                                                    snake As SnakeAnimal,
                                                    circle As CircleShape,
                                                    rect As RectShape,
                                                    repeated As String,
                                                    long2k As String,
                                                    long8k As String,
                                                    expando As ExpandoObject) As Dictionary(Of String, Object)

            Dim lvl3a As New Dictionary(Of String, Object) From {
                {"k", repeated},
                {"n", 999},
                {"obj", dog},
                {"arr", New Object() {cat, Nothing, "x", New Integer() {1, 2, 3}, New List(Of Object) From {snake, repeated}}},
                {"bytes", New Byte() {9, 9, 9, 9}},
                {"deepText", long2k}
            }

            Dim lvl3b As New Dictionary(Of String, Object) From {
                {"shape", circle},
                {"shape2", rect},
                {"list", New List(Of Object) From {1, 2, 3, "four", Nothing, repeated, expando}},
                {"map", New Dictionary(Of String, Object) From {{"a", 1}, {"b", "two"}, {"c", dog}}},
                {"long", long8k}
            }

            Dim lvl2 As New Dictionary(Of String, Object) From {
                {"lvl3a", lvl3a},
                {"lvl3b", lvl3b},
                {"animals", New List(Of AnimalBase) From {dog, cat, snake, Nothing}},
                {"mix", New Object() {lvl3a, lvl3b, dog, "x", Nothing, New Byte() {1, 2, 3}}},
                {"repeatA", repeated},
                {"repeatB", repeated},
                {"repeatC", repeated}
            }

            Dim lvl1 As New Dictionary(Of String, Object) From {
                {"lvl2", lvl2},
                {"mirror", New Dictionary(Of String, Object) From {{"lvl2", lvl2}}}, ' shared reference (não é ciclo)
                {"nulls", New Object() {Nothing, Nothing, "x", Nothing}},
                {"ints", New Integer() {1, -2, 3, Integer.MaxValue, Integer.MinValue}},
                {"dates", New DateTime?() {Utc(2025, 1, 1, 0, 0, 0), Nothing, Utc(2025, 2, 2, 2, 2, 2)}}
            }

            Return lvl1
        End Function

        Private Shared Function GuidLikeId(len As Integer) As String
            Dim sb As New StringBuilder(len)
            Dim chars As String = "abcdef0123456789"
            Dim r As New Random(12345)
            For i As Integer = 1 To len
                sb.Append(chars(r.Next(0, chars.Length)))
            Next
            Return sb.ToString()
        End Function

        Private Shared Function Utc(y As Integer, m As Integer, d As Integer, hh As Integer, mm As Integer, ss As Integer) As DateTime
            Return New DateTime(y, m, d, hh, mm, ss, DateTimeKind.Utc)
        End Function

#Region "Templates (heavy)"

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

        Private Class SnakeAnimal
            Inherits AnimalBase
            Public Property Venom As Boolean
            Public Property LengthCm As Integer
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

        Private Class HeavyContainer
            Public Property Id As String
            Public Property Name As String
            Public Property CreatedUtc As DateTime

            Public Property Primary As AnimalBase
            Public Property Secondary As AnimalBase
            Public Property AllAnimals As List(Of AnimalBase)

            Public Property AnyMap As Dictionary(Of String, Object)
            Public Property AnyList As List(Of Object)
            Public Property AnyQueue As Queue(Of Object)
            Public Property AnyStack As Stack(Of Object)
            Public Property AnyLinked As LinkedList(Of Object)

            Public Property Flags As Dictionary(Of String, Boolean?)
            Public Property Numbers As Integer?()
            Public Property Doubles As Double?()

            Public Property Payloads As List(Of Byte())
        End Class

#End Region

    End Class

End Namespace