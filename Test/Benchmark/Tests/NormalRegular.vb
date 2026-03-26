Option Explicit On
Option Strict On
Option Infer On

Imports System
Imports System.Collections.Generic

Namespace Benchmark.Tests

    Public Class NormalRegular
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "NormalRegular"
            End Get
        End Property

        Public Overrides ReadOnly Property Threads As Integer
            Get
                Return Environment.ProcessorCount
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Realistic business-like nested objects: Clients -> Purchases -> Items -> Product + Address/Shipping/flags/preferences. Randomized but deterministic (seed)."
            End Get
        End Property

        ' Ajuste se quiser mais/menos trabalho por rodada:
        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 10
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            ' Dataset “razoável” (ajuste livre):
            Const clientCount As Integer = 10
            Const purchasesPerClient As Integer = 5
            Const itemsPerPurchase As Integer = 4
            Const seed As Integer = 123456

            Dim ds As ClientDataset = GenerateDataset(
                clientCount:=clientCount,
                purchasesPerClient:=purchasesPerClient,
                itemsPerPurchase:=itemsPerPurchase,
                seed:=seed
            )

            ' Seu decoder sempre devolve Dictionary(Of String,Object) no root, então
            ' qualquer root “objeto” é OK (ele vira dict).
            Return New List(Of Object) From {ds}

        End Function

#Region "Generator"

        Private Shared Function GenerateDataset(clientCount As Integer,
                                               purchasesPerClient As Integer,
                                               itemsPerPurchase As Integer,
                                               seed As Integer) As ClientDataset

            Dim rng As New Random(seed)

            Dim ds As New ClientDataset With {
                .DatasetId = GuidLikeId(rng, 24),
                .GeneratedUtc = UtcNowMinus(rng, daysBackMax:=30),
                .CurrencyDefault = Pick(rng, Currencies),
                .Clients = New List(Of Client)(clientCount)
            }

            ' Catálogo compartilhado (reuso de Product objects)
            Dim catalog As New Dictionary(Of String, Product)(StringComparer.Ordinal)

            For i As Integer = 1 To clientCount
                Dim c As Client = BuildClient(rng, i, catalog, purchasesPerClient, itemsPerPurchase, ds.CurrencyDefault)
                ds.Clients.Add(c)
            Next

            ' Métricas (só pra ter mais dados “normais”)
            ds.TotalClients = ds.Clients.Count
            ds.TotalPurchases = ds.Clients.Sum(Function(x) If(x.Purchases Is Nothing, 0, x.Purchases.Count))
            ds.TotalItems = ds.Clients.Sum(Function(x) SumItems(x))

            Return ds
        End Function

        Private Shared Function SumItems(c As Client) As Integer
            If c Is Nothing OrElse c.Purchases Is Nothing Then Return 0
            Dim n As Integer = 0
            For Each p In c.Purchases
                If p Is Nothing OrElse p.Items Is Nothing Then Continue For
                n += p.Items.Count
            Next
            Return n
        End Function

        Private Shared Function BuildClient(rng As Random,
                                            clientSeq As Integer,
                                            catalog As Dictionary(Of String, Product),
                                            purchasesPerClient As Integer,
                                            itemsPerPurchase As Integer,
                                            defaultCurrency As String) As Client

            Dim first As String = Pick(rng, FirstNames)
            Dim last As String = Pick(rng, LastNames)
            Dim fullName As String = first & " " & last

            Dim email As String =
                (first & "." & last & clientSeq.ToString() & "@" & Pick(rng, EmailDomains)).ToLowerInvariant()

            Dim addr As Address = BuildAddress(rng)

            Dim c As New Client With {
                .ClientId = clientSeq,
                .Name = fullName,
                .Email = email,
                .IsActive = (rng.NextDouble() >= 0.08), ' ~8% inativos
                .CreatedUtc = UtcNowMinus(rng, daysBackMax:=365 * 4),
                .Tier = CType(rng.Next(0, 4), ClientTier),
                .Address = addr,
                .Tags = BuildTags(rng, maxCount:=6),
                .Preferences = New Dictionary(Of String, String)(StringComparer.Ordinal),
                .Flags = New Dictionary(Of String, Boolean?)(StringComparer.Ordinal),
                .Purchases = New List(Of Purchase)(purchasesPerClient)
            }

            ' Preferências “normais”
            c.Preferences("lang") = Pick(rng, Languages)
            c.Preferences("tz") = Pick(rng, Timezones)
            c.Preferences("currency") = If(rng.NextDouble() < 0.7, defaultCurrency, Pick(rng, Currencies))
            c.Preferences("theme") = Pick(rng, Themes)

            ' Flags com nulls realistas
            c.Flags("marketingEmail") = If(rng.NextDouble() < 0.1, CType(Nothing, Boolean?), rng.NextDouble() < 0.65)
            c.Flags("marketingSms") = If(rng.NextDouble() < 0.15, CType(Nothing, Boolean?), rng.NextDouble() < 0.25)
            c.Flags("betaUser") = If(rng.NextDouble() < 0.05, CType(Nothing, Boolean?), rng.NextDouble() < 0.05)

            ' Compras
            For pi As Integer = 1 To purchasesPerClient
                Dim p As Purchase = BuildPurchase(rng, c, catalog, itemsPerPurchase, defaultCurrency)
                c.Purchases.Add(p)
            Next

            ' Série simples “de uso” (12 meses) com alguns nulls
            c.MonthlySpend = New Double?(11) {}
            For m As Integer = 0 To 11
                If rng.NextDouble() < 0.08 Then
                    c.MonthlySpend(m) = Nothing
                Else
                    c.MonthlySpend(m) = Math.Round(rng.NextDouble() * 1200.0R, 2)
                End If
            Next

            Return c
        End Function

        Private Shared Function BuildPurchase(rng As Random,
                                              c As Client,
                                              catalog As Dictionary(Of String, Product),
                                              itemsPerPurchase As Integer,
                                              defaultCurrency As String) As Purchase

            Dim purchasedUtc As DateTime = UtcNowMinus(rng, daysBackMax:=365)
            Dim currency As String = If(rng.NextDouble() < 0.75, defaultCurrency, Pick(rng, Currencies))

            Dim p As New Purchase With {
                .OrderId = "ORD-" & GuidLikeId(rng, 16),
                .PurchasedUtc = purchasedUtc,
                .Currency = currency,
                .Coupon = If(rng.NextDouble() < 0.18, Pick(rng, Coupons), Nothing),
                .Items = New List(Of PurchaseItem)(itemsPerPurchase),
                .Metadata = New Dictionary(Of String, String)(StringComparer.Ordinal)
            }

            ' Shipping
            p.Shipping = New ShippingInfo With {
                .Method = Pick(rng, ShippingMethods),
                .Price = Math.Round(rng.NextDouble() * 40.0R, 2),
                .AddressSnapshot = c.Address,
                .DeliveredUtc = If(rng.NextDouble() < 0.15, CType(Nothing, DateTime?), purchasedUtc.AddDays(rng.Next(1, 12)))
            }

            Dim total As Double = 0

            For ii As Integer = 1 To itemsPerPurchase
                Dim it As PurchaseItem = BuildItem(rng, catalog)
                p.Items.Add(it)
                total += (it.UnitPrice * it.Quantity) - it.Discount
            Next

            ' Taxes/Total
            p.Tax = Math.Round(total * (0.05R + rng.NextDouble() * 0.12R), 2) ' 5%..17%
            p.Total = Math.Round(total + p.Tax + p.Shipping.Price, 2)

            ' Metadata “normal”
            p.Metadata("channel") = Pick(rng, Channels)
            p.Metadata("device") = Pick(rng, Devices)
            p.Metadata("store") = Pick(rng, Stores)

            Return p
        End Function

        Private Shared Function BuildItem(rng As Random, catalog As Dictionary(Of String, Product)) As PurchaseItem

            ' Produto reaproveitado (shared objects)
            Dim sku As String = "SKU-" & rng.Next(1000, 99999).ToString()
            Dim prod As Product = Nothing

            If Not catalog.TryGetValue(sku, prod) Then
                prod = New Product With {
                    .Sku = sku,
                    .Name = Pick(rng, ProductNames),
                    .Category = Pick(rng, Categories),
                    .Tags = BuildTags(rng, maxCount:=5)
                }
                catalog(sku) = prod
            End If

            Dim qty As Integer = If(rng.NextDouble() < 0.85, 1, rng.Next(2, 6))
            Dim unitPrice As Double = Math.Round(5.0R + rng.NextDouble() * 250.0R, 2)

            Dim discount As Double =
                If(rng.NextDouble() < 0.22, Math.Round(rng.NextDouble() * (unitPrice * 0.25R), 2), 0.0R)

            Dim it As New PurchaseItem With {
                .Product = prod,
                .Quantity = qty,
                .UnitPrice = unitPrice,
                .Discount = discount,
                .Note = If(rng.NextDouble() < 0.08, Pick(rng, Notes), Nothing)
            }

            Return it
        End Function

        Private Shared Function BuildAddress(rng As Random) As Address
            Return New Address With {
                .Country = "BR",
                .State = Pick(rng, StatesBR),
                .City = Pick(rng, CitiesBR),
                .Street = Pick(rng, Streets),
                .Number = rng.Next(1, 5000),
                .Zip = rng.Next(10000000, 99999999).ToString()
            }
        End Function

        Private Shared Function BuildTags(rng As Random, maxCount As Integer) As List(Of String)
            Dim count As Integer = rng.Next(0, maxCount + 1)
            If count = 0 Then Return New List(Of String)()

            Dim tags As New List(Of String)(count)
            For i As Integer = 1 To count
                ' pool fixo => repete bastante (bom pro string-table)
                tags.Add(Pick(rng, TagPool))
            Next
            Return tags
        End Function

        Private Shared Function Pick(rng As Random, arr As String()) As String
            Return arr(rng.Next(0, arr.Length))
        End Function

        Private Shared Function GuidLikeId(rng As Random, len As Integer) As String
            Const chars As String = "abcdef0123456789"
            Dim sb As New System.Text.StringBuilder(len)
            For i As Integer = 1 To len
                sb.Append(chars(rng.Next(0, chars.Length)))
            Next
            Return sb.ToString()
        End Function

        Private Shared Function UtcNowMinus(rng As Random, daysBackMax As Integer) As DateTime
            Dim days As Integer = rng.Next(0, Math.Max(1, daysBackMax + 1))
            Dim seconds As Integer = rng.Next(0, 24 * 3600)
            Return DateTime.UtcNow.AddDays(-days).AddSeconds(-seconds)
        End Function

#End Region

#Region "Templates"

        Private Enum ClientTier As Integer
            Free = 0
            Silver = 1
            Gold = 2
            Platinum = 3
        End Enum

        Private Class ClientDataset
            Public Property DatasetId As String
            Public Property GeneratedUtc As DateTime
            Public Property CurrencyDefault As String

            Public Property TotalClients As Integer
            Public Property TotalPurchases As Integer
            Public Property TotalItems As Integer

            Public Property Clients As List(Of Client)
        End Class

        Private Class Client
            Public Property ClientId As Integer
            Public Property Name As String
            Public Property Email As String
            Public Property IsActive As Boolean
            Public Property CreatedUtc As DateTime
            Public Property Tier As ClientTier

            Public Property Address As Address
            Public Property Tags As List(Of String)

            Public Property Preferences As Dictionary(Of String, String)
            Public Property Flags As Dictionary(Of String, Boolean?)

            Public Property MonthlySpend As Double?()
            Public Property Purchases As List(Of Purchase)
        End Class

        Private Class Address
            Public Property Country As String
            Public Property State As String
            Public Property City As String
            Public Property Street As String
            Public Property Number As Integer
            Public Property Zip As String
        End Class

        Private Class Purchase
            Public Property OrderId As String
            Public Property PurchasedUtc As DateTime
            Public Property Currency As String

            Public Property Coupon As String
            Public Property Tax As Double
            Public Property Total As Double

            Public Property Shipping As ShippingInfo
            Public Property Items As List(Of PurchaseItem)

            Public Property Metadata As Dictionary(Of String, String)
        End Class

        Private Class ShippingInfo
            Public Property Method As String
            Public Property Price As Double
            Public Property DeliveredUtc As DateTime?

            Public Property AddressSnapshot As Address
        End Class

        Private Class PurchaseItem
            Public Property Product As Product
            Public Property Quantity As Integer
            Public Property UnitPrice As Double
            Public Property Discount As Double
            Public Property Note As String
        End Class

        Private Class Product
            Public Property Sku As String
            Public Property Name As String
            Public Property Category As String
            Public Property Tags As List(Of String)
        End Class

#End Region

#Region "Data pools"

        Private Shared ReadOnly FirstNames As String() =
            {"Alice", "Bob", "Mark", "Julia", "Carla", "Rafa", "Pedro", "Mia", "Noah", "Eva", "Liam", "Sara", "Bruno", "Nina"}

        Private Shared ReadOnly LastNames As String() =
            {"Silva", "Souza", "Costa", "Santos", "Oliveira", "Pereira", "Lima", "Almeida", "Rocha", "Gomes", "Ribeiro"}

        Private Shared ReadOnly EmailDomains As String() =
            {"example.com", "mail.com", "corp.local", "acme.io", "foo.bar"}

        Private Shared ReadOnly Currencies As String() =
            {"BRL", "USD", "EUR"}

        Private Shared ReadOnly Languages As String() =
            {"pt-BR", "en-US", "es-ES"}

        Private Shared ReadOnly Timezones As String() =
            {"America/Recife", "America/Sao_Paulo", "UTC", "America/New_York"}

        Private Shared ReadOnly Themes As String() =
            {"light", "dark", "system"}

        Private Shared ReadOnly Coupons As String() =
            {"WELCOME10", "FREESHIP", "VIP20", "BR5OFF", "SPRING"}

        Private Shared ReadOnly ShippingMethods As String() =
            {"standard", "express", "pickup"}

        Private Shared ReadOnly Channels As String() =
            {"web", "mobile", "partner"}

        Private Shared ReadOnly Devices As String() =
            {"android", "ios", "windows", "linux", "mac"}

        Private Shared ReadOnly Stores As String() =
            {"main", "outlet", "marketplace"}

        Private Shared ReadOnly ProductNames As String() =
            {"T-Shirt", "Sneakers", "Headphones", "Backpack", "Bottle", "Keyboard", "Mouse", "Monitor", "Book", "Coffee"}

        Private Shared ReadOnly Categories As String() =
            {"fashion", "electronics", "home", "sports", "books", "food"}

        Private Shared ReadOnly TagPool As String() =
            {"promo", "new", "hot", "gift", "eco", "premium", "basic", "limited", "sale", "bundle", "cache-me"}

        Private Shared ReadOnly Notes As String() =
            {"gift wrap", "deliver after 18h", "leave at reception", "fragile", "call on arrival"}

        Private Shared ReadOnly StatesBR As String() =
            {"PE", "SP", "RJ", "MG", "BA", "RS", "CE"}

        Private Shared ReadOnly CitiesBR As String() =
            {"Recife", "São Paulo", "Rio de Janeiro", "Belo Horizonte", "Salvador", "Porto Alegre", "Fortaleza"}

        Private Shared ReadOnly Streets As String() =
            {"Av. Central", "Rua das Flores", "Rua do Sol", "Av. Brasil", "Rua A", "Rua B", "Rua C"}

#End Region

    End Class

End Namespace