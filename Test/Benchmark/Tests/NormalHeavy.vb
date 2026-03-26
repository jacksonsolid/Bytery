Option Explicit On
Option Strict On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.Linq

Namespace Benchmark.Tests

    Public Class NormalHeavy
        Inherits Test

        Public Overrides ReadOnly Property Name As String
            Get
                Return "NormalHeavy"
            End Get
        End Property

        Public Overrides ReadOnly Property Threads As Integer
            Get
                Return 2
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Heavy realistic payload: larger Clients dataset with Purchases/Items, Payment/Shipping histories, device profiles, balances, multiple addresses and richer metadata. Randomized but deterministic (seed + fixed baseUtc)."
            End Get
        End Property

        ' Heavy => menos iterações (cada iteração já é grande).
        Public Overrides ReadOnly Property Iterations As Integer
            Get
                Return 10
            End Get
        End Property

        Public Overrides Function BuildObjects() As List(Of Object)

            ' Ajuste livre (pesado, porém ainda “realista”):
            Const clientCount As Integer = 200
            Const purchasesPerClient As Integer = 10
            Const itemsPerPurchase As Integer = 5

            Const seed As Integer = 987654
            Dim baseUtc As New DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc) ' fixo => determinístico

            Dim ds As HeavyDataset = GenerateDataset(
                clientCount:=clientCount,
                purchasesPerClient:=purchasesPerClient,
                itemsPerPurchase:=itemsPerPurchase,
                seed:=seed,
                baseUtc:=baseUtc
            )

            Return New List(Of Object) From {ds}

        End Function

#Region "Generator"

        Private Shared Function GenerateDataset(clientCount As Integer,
                                               purchasesPerClient As Integer,
                                               itemsPerPurchase As Integer,
                                               seed As Integer,
                                               baseUtc As DateTime) As HeavyDataset

            Dim rng As New Random(seed)

            Dim ds As New HeavyDataset With {
                .DatasetId = GuidLikeId(rng, 28),
                .GeneratedUtc = UtcMinus(rng, baseUtc, daysBackMax:=7),
                .CurrencyDefault = Pick(rng, Currencies),
                .SourceSystem = Pick(rng, SourceSystems),
                .CatalogVersion = "CAT-" & rng.Next(100, 999).ToString(),
                .BatchNumber = rng.Next(1, 999999),
                .Metadata = New Dictionary(Of String, String)(StringComparer.Ordinal),
                .Clients = New List(Of Client)(clientCount)
            }

            ds.Metadata("env") = Pick(rng, Envs)
            ds.Metadata("region") = Pick(rng, Regions)
            ds.Metadata("build") = "b" & rng.Next(1000, 9999).ToString()
            ds.Metadata("pipeline") = Pick(rng, Pipelines)

            ' Catálogo compartilhado (reuso de Product objects)
            Dim catalog As New Dictionary(Of String, Product)(StringComparer.Ordinal)

            For i As Integer = 1 To clientCount
                ds.Clients.Add(BuildClient(rng, i, catalog, purchasesPerClient, itemsPerPurchase, ds.CurrencyDefault, baseUtc))
            Next

            ' Totais (loop, sem LINQ pesado)
            Dim totalPurchases As Integer = 0
            Dim totalItems As Integer = 0
            For Each c In ds.Clients
                If c Is Nothing OrElse c.Purchases Is Nothing Then Continue For
                totalPurchases += c.Purchases.Count
                For Each p In c.Purchases
                    If p Is Nothing OrElse p.Items Is Nothing Then Continue For
                    totalItems += p.Items.Count
                Next
            Next

            ds.TotalClients = ds.Clients.Count
            ds.TotalPurchases = totalPurchases
            ds.TotalItems = totalItems

            Return ds
        End Function

        Private Shared Function BuildClient(rng As Random,
                                            clientSeq As Integer,
                                            catalog As Dictionary(Of String, Product),
                                            purchasesPerClient As Integer,
                                            itemsPerPurchase As Integer,
                                            defaultCurrency As String,
                                            baseUtc As DateTime) As Client

            Dim first As String = Pick(rng, FirstNames)
            Dim last As String = Pick(rng, LastNames)
            Dim fullName As String = first & " " & last

            Dim email As String =
                (first & "." & last & clientSeq.ToString() & "@" & Pick(rng, EmailDomains)).ToLowerInvariant()

            Dim primaryAddr As Address = BuildAddress(rng)

            Dim c As New Client With {
                .ClientId = clientSeq,
                .ExternalId = "C-" & GuidLikeId(rng, 18),
                .Name = fullName,
                .Email = email,
                .Phone = "+55" & rng.Next(10, 99).ToString() & rng.Next(900000000, 999999999).ToString(),
                .DocumentId = rng.Next(100000000, 999999999).ToString() & rng.Next(10, 99).ToString(),
                .IsActive = (rng.NextDouble() >= 0.06),
                .CreatedUtc = UtcMinus(rng, baseUtc, daysBackMax:=365 * 6),
                .LastLoginUtc = If(rng.NextDouble() < 0.03, CType(Nothing, DateTime?), UtcMinus(rng, baseUtc, daysBackMax:=60)),
                .Tier = CType(rng.Next(0, 4), ClientTier),
                .PrimaryAddress = primaryAddr,
                .Addresses = New List(Of Address)(capacity:=3),
                .Tags = BuildTags(rng, maxCount:=10),
                .Segments = BuildSegments(rng, maxCount:=4),
                .Preferences = New Dictionary(Of String, String)(StringComparer.Ordinal),
                .Flags = New Dictionary(Of String, Boolean?)(StringComparer.Ordinal),
                .Balances = New Dictionary(Of String, Double?)(StringComparer.Ordinal),
                .Devices = New List(Of DeviceProfile)(capacity:=rng.Next(1, 4)),
                .Loyalty = New LoyaltyInfo(),
                .Purchases = New List(Of Purchase)(purchasesPerClient)
            }

            ' Addresses (além da primária, com reuso ocasional)
            c.Addresses.Add(primaryAddr)
            If rng.NextDouble() < 0.7 Then c.Addresses.Add(BuildAddress(rng))
            If rng.NextDouble() < 0.25 Then c.Addresses.Add(BuildAddress(rng))

            ' Preferências “normais”
            c.Preferences("lang") = Pick(rng, Languages)
            c.Preferences("tz") = Pick(rng, Timezones)
            c.Preferences("currency") = If(rng.NextDouble() < 0.8, defaultCurrency, Pick(rng, Currencies))
            c.Preferences("theme") = Pick(rng, Themes)
            c.Preferences("notif") = Pick(rng, NotifModes)

            ' Flags com nulls realistas
            c.Flags("marketingEmail") = If(rng.NextDouble() < 0.08, CType(Nothing, Boolean?), rng.NextDouble() < 0.65)
            c.Flags("marketingSms") = If(rng.NextDouble() < 0.12, CType(Nothing, Boolean?), rng.NextDouble() < 0.25)
            c.Flags("betaUser") = If(rng.NextDouble() < 0.04, CType(Nothing, Boolean?), rng.NextDouble() < 0.06)
            c.Flags("fraudWatch") = If(rng.NextDouble() < 0.06, CType(Nothing, Boolean?), rng.NextDouble() < 0.03)

            ' Balances (wallets)
            c.Balances("main") = Math.Round(rng.NextDouble() * 5000.0R, 2)
            c.Balances("bonus") = If(rng.NextDouble() < 0.15, CType(Nothing, Double?), Math.Round(rng.NextDouble() * 300.0R, 2))
            c.Balances("credit") = If(rng.NextDouble() < 0.2, Math.Round(rng.NextDouble() * 2000.0R, 2), 0.0R)

            ' Devices
            Dim devCount As Integer = rng.Next(1, 4)
            For i As Integer = 1 To devCount
                c.Devices.Add(New DeviceProfile With {
                    .DeviceId = "D-" & GuidLikeId(rng, 14),
                    .DeviceType = Pick(rng, Devices),
                    .Os = Pick(rng, OSes),
                    .AppVersion = rng.Next(1, 9).ToString() & "." & rng.Next(0, 30).ToString() & "." & rng.Next(0, 200).ToString(),
                    .LastSeenUtc = UtcMinus(rng, baseUtc, daysBackMax:=45),
                    .Ip = $"{rng.Next(10, 240)}.{rng.Next(0, 255)}.{rng.Next(0, 255)}.{rng.Next(0, 255)}"
                })
            Next

            ' Loyalty
            c.Loyalty.Program = Pick(rng, LoyaltyPrograms)
            c.Loyalty.Points = rng.Next(0, 250000)
            c.Loyalty.Level = Pick(rng, LoyaltyLevels)
            c.Loyalty.JoinedUtc = UtcMinus(rng, baseUtc, daysBackMax:=365 * 5)

            ' Série mensal (24 meses) com alguns nulls
            c.MonthlySpend = New Double?(23) {}
            For m As Integer = 0 To 23
                If rng.NextDouble() < 0.06 Then
                    c.MonthlySpend(m) = Nothing
                Else
                    c.MonthlySpend(m) = Math.Round(rng.NextDouble() * 2200.0R, 2)
                End If
            Next

            ' Purchases
            For pi As Integer = 1 To purchasesPerClient
                c.Purchases.Add(BuildPurchase(rng, c, catalog, itemsPerPurchase, defaultCurrency, baseUtc))
            Next

            Return c
        End Function

        Private Shared Function BuildPurchase(rng As Random,
                                              c As Client,
                                              catalog As Dictionary(Of String, Product),
                                              itemsPerPurchase As Integer,
                                              defaultCurrency As String,
                                              baseUtc As DateTime) As Purchase

            Dim purchasedUtc As DateTime = UtcMinus(rng, baseUtc, daysBackMax:=365)
            Dim currency As String = If(rng.NextDouble() < 0.78, defaultCurrency, Pick(rng, Currencies))

            Dim p As New Purchase With {
                .OrderId = "ORD-" & GuidLikeId(rng, 18),
                .PurchasedUtc = purchasedUtc,
                .Currency = currency,
                .Status = Pick(rng, OrderStatuses),
                .Coupon = If(rng.NextDouble() < 0.22, Pick(rng, Coupons), Nothing),
                .Items = New List(Of PurchaseItem)(itemsPerPurchase),
                .Metadata = New Dictionary(Of String, String)(StringComparer.Ordinal),
                .Payment = New PaymentInfo With {.Method = Pick(rng, PaymentMethods)},
                .Shipping = New ShippingInfo With {
                    .Method = Pick(rng, ShippingMethods),
                    .Carrier = Pick(rng, Carriers),
                    .Price = Math.Round(rng.NextDouble() * 55.0R, 2),
                    .AddressSnapshot = c.PrimaryAddress,
                    .TrackingCode = If(rng.NextDouble() < 0.75, "TRK-" & GuidLikeId(rng, 14), Nothing),
                    .DeliveredUtc = If(rng.NextDouble() < 0.2, CType(Nothing, DateTime?), purchasedUtc.AddDays(rng.Next(1, 18))),
                    .StatusHistory = New List(Of ShipStatus)(capacity:=rng.Next(2, 6))
                }
            }

            ' Shipping status history
            Dim steps As Integer = rng.Next(2, 6)
            Dim t As DateTime = purchasedUtc
            For i As Integer = 1 To steps
                t = t.AddHours(rng.Next(4, 48))
                p.Shipping.StatusHistory.Add(New ShipStatus With {.WhenUtc = t, .Status = Pick(rng, ShipStatuses)})
            Next

            ' Payment
            p.Payment.Currency = currency
            p.Payment.PaidUtc = If(rng.NextDouble() < 0.04, CType(Nothing, DateTime?), purchasedUtc.AddMinutes(rng.Next(1, 180)))
            p.Payment.AuthCode = "AUTH-" & rng.Next(100000, 999999).ToString()
            p.Payment.Installments = If(rng.NextDouble() < 0.6, 1, rng.Next(2, 13))
            p.Payment.CardLast4 = rng.Next(0, 10000).ToString("0000")
            p.Payment.Metadata = New Dictionary(Of String, String)(StringComparer.Ordinal)
            p.Payment.Metadata("psp") = Pick(rng, Psps)
            p.Payment.Metadata("country") = "BR"
            p.Payment.Metadata("risk") = Pick(rng, RiskBands)

            ' Items + total
            Dim total As Double = 0
            For ii As Integer = 1 To itemsPerPurchase
                Dim it As PurchaseItem = BuildItem(rng, catalog)
                p.Items.Add(it)
                total += (it.UnitPrice * it.Quantity) - it.Discount
            Next

            p.Tax = Math.Round(total * (0.06R + rng.NextDouble() * 0.14R), 2) ' 6%..20%
            p.Total = Math.Round(total + p.Tax + p.Shipping.Price, 2)

            ' Metadata “normal”
            p.Metadata("channel") = Pick(rng, Channels)
            p.Metadata("device") = Pick(rng, Devices)
            p.Metadata("store") = Pick(rng, Stores)
            p.Metadata("session") = GuidLikeId(rng, 16)

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
                    .Brand = Pick(rng, Brands),
                    .Tags = BuildTags(rng, maxCount:=7),
                    .Attributes = New Dictionary(Of String, String)(StringComparer.Ordinal)
                }
                prod.Attributes("color") = Pick(rng, Colors)
                prod.Attributes("size") = Pick(rng, Sizes)
                prod.Attributes("material") = Pick(rng, Materials)

                catalog(sku) = prod
            End If

            Dim qty As Integer = If(rng.NextDouble() < 0.82, 1, rng.Next(2, 8))
            Dim unitPrice As Double = Math.Round(8.0R + rng.NextDouble() * 420.0R, 2)
            Dim discount As Double = If(rng.NextDouble() < 0.24, Math.Round(rng.NextDouble() * (unitPrice * 0.28R), 2), 0.0R)

            Dim it As New PurchaseItem With {
                .Product = prod,
                .Quantity = qty,
                .UnitPrice = unitPrice,
                .Discount = discount,
                .WeightKg = Math.Round(rng.NextDouble() * 4.5R, 3),
                .IsGift = If(rng.NextDouble() < 0.07, CType(Nothing, Boolean?), rng.NextDouble() < 0.08),
                .Note = If(rng.NextDouble() < 0.1, Pick(rng, Notes), Nothing),
                .Attributes = New Dictionary(Of String, String)(StringComparer.Ordinal)
            }

            it.Attributes("giftWrap") = Pick(rng, GiftWrap)
            it.Attributes("warehouse") = Pick(rng, Warehouses)

            Return it
        End Function

        Private Shared Function BuildAddress(rng As Random) As Address
            Return New Address With {
                .Country = "BR",
                .State = Pick(rng, StatesBR),
                .City = Pick(rng, CitiesBR),
                .Street = Pick(rng, Streets),
                .Number = rng.Next(1, 8000),
                .Zip = rng.Next(10000000, 99999999).ToString(),
                .Complement = If(rng.NextDouble() < 0.35, "Apt " & rng.Next(1, 2000).ToString(), Nothing)
            }
        End Function

        Private Shared Function BuildTags(rng As Random, maxCount As Integer) As List(Of String)
            Dim count As Integer = rng.Next(0, maxCount + 1)
            If count = 0 Then Return New List(Of String)()

            Dim tags As New List(Of String)(count)
            For i As Integer = 1 To count
                tags.Add(Pick(rng, TagPool))
            Next
            Return tags
        End Function

        Private Shared Function BuildSegments(rng As Random, maxCount As Integer) As List(Of String)
            Dim count As Integer = rng.Next(0, maxCount + 1)
            If count = 0 Then Return New List(Of String)()
            Dim segs As New List(Of String)(count)
            For i As Integer = 1 To count
                segs.Add(Pick(rng, Segments))
            Next
            Return segs
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

        Private Shared Function UtcMinus(rng As Random, baseUtc As DateTime, daysBackMax As Integer) As DateTime
            Dim days As Integer = rng.Next(0, Math.Max(1, daysBackMax + 1))
            Dim seconds As Integer = rng.Next(0, 24 * 3600)
            Return baseUtc.AddDays(-days).AddSeconds(-seconds)
        End Function

#End Region

#Region "Templates"

        Private Enum ClientTier As Integer
            Free = 0
            Silver = 1
            Gold = 2
            Platinum = 3
        End Enum

        Private Class HeavyDataset
            Public Property DatasetId As String
            Public Property GeneratedUtc As DateTime
            Public Property CurrencyDefault As String
            Public Property SourceSystem As String
            Public Property CatalogVersion As String
            Public Property BatchNumber As Integer
            Public Property Metadata As Dictionary(Of String, String)

            Public Property TotalClients As Integer
            Public Property TotalPurchases As Integer
            Public Property TotalItems As Integer

            Public Property Clients As List(Of Client)
        End Class

        Private Class Client
            Public Property ClientId As Integer
            Public Property ExternalId As String
            Public Property Name As String
            Public Property Email As String
            Public Property Phone As String
            Public Property DocumentId As String

            Public Property IsActive As Boolean
            Public Property CreatedUtc As DateTime
            Public Property LastLoginUtc As DateTime?
            Public Property Tier As ClientTier

            Public Property PrimaryAddress As Address
            Public Property Addresses As List(Of Address)

            Public Property Tags As List(Of String)
            Public Property Segments As List(Of String)

            Public Property Preferences As Dictionary(Of String, String)
            Public Property Flags As Dictionary(Of String, Boolean?)
            Public Property Balances As Dictionary(Of String, Double?)

            Public Property Devices As List(Of DeviceProfile)
            Public Property Loyalty As LoyaltyInfo

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
            Public Property Complement As String
        End Class

        Private Class DeviceProfile
            Public Property DeviceId As String
            Public Property DeviceType As String
            Public Property Os As String
            Public Property AppVersion As String
            Public Property LastSeenUtc As DateTime
            Public Property Ip As String
        End Class

        Private Class LoyaltyInfo
            Public Property Program As String
            Public Property Level As String
            Public Property Points As Integer
            Public Property JoinedUtc As DateTime
        End Class

        Private Class Purchase
            Public Property OrderId As String
            Public Property PurchasedUtc As DateTime
            Public Property Currency As String
            Public Property Status As String

            Public Property Coupon As String
            Public Property Tax As Double
            Public Property Total As Double

            Public Property Payment As PaymentInfo
            Public Property Shipping As ShippingInfo
            Public Property Items As List(Of PurchaseItem)

            Public Property Metadata As Dictionary(Of String, String)
        End Class

        Private Class PaymentInfo
            Public Property Method As String
            Public Property Currency As String
            Public Property PaidUtc As DateTime?
            Public Property AuthCode As String
            Public Property Installments As Integer
            Public Property CardLast4 As String
            Public Property Metadata As Dictionary(Of String, String)
        End Class

        Private Class ShippingInfo
            Public Property Method As String
            Public Property Carrier As String
            Public Property Price As Double
            Public Property DeliveredUtc As DateTime?
            Public Property TrackingCode As String
            Public Property AddressSnapshot As Address
            Public Property StatusHistory As List(Of ShipStatus)
        End Class

        Private Class ShipStatus
            Public Property WhenUtc As DateTime
            Public Property Status As String
        End Class

        Private Class PurchaseItem
            Public Property Product As Product
            Public Property Quantity As Integer
            Public Property UnitPrice As Double
            Public Property Discount As Double
            Public Property WeightKg As Double
            Public Property IsGift As Boolean?
            Public Property Note As String
            Public Property Attributes As Dictionary(Of String, String)
        End Class

        Private Class Product
            Public Property Sku As String
            Public Property Name As String
            Public Property Category As String
            Public Property Brand As String
            Public Property Tags As List(Of String)
            Public Property Attributes As Dictionary(Of String, String)
        End Class

#End Region

#Region "Data pools"

        ' ---- Missing pools (add to #Region "Data pools") ----

        Private Shared ReadOnly OrderStatuses As String() =
            {"created", "paid", "processing", "shipped", "delivered", "canceled", "refunded", "chargeback"}

        Private Shared ReadOnly LoyaltyPrograms As String() =
            {"standard", "plus", "vip"}

        Private Shared ReadOnly LoyaltyLevels As String() =
            {"bronze", "silver", "gold", "platinum"}

        Private Shared ReadOnly FirstNames As String() =
            {"Alice", "Bob", "Mark", "Julia", "Carla", "Rafa", "Pedro", "Mia", "Noah", "Eva", "Liam", "Sara", "Bruno", "Nina", "Arthur", "Laura", "Diego", "Sofia"}

        Private Shared ReadOnly LastNames As String() =
            {"Silva", "Souza", "Costa", "Santos", "Oliveira", "Pereira", "Lima", "Almeida", "Rocha", "Gomes", "Ribeiro", "Carvalho", "Barbosa"}

        Private Shared ReadOnly EmailDomains As String() =
            {"example.com", "mail.com", "corp.local", "acme.io", "foo.bar", "shop.test"}

        Private Shared ReadOnly Currencies As String() =
            {"BRL", "USD", "EUR"}

        Private Shared ReadOnly Languages As String() =
            {"pt-BR", "en-US", "es-ES"}

        Private Shared ReadOnly Timezones As String() =
            {"America/Recife", "America/Sao_Paulo", "UTC", "America/New_York"}

        Private Shared ReadOnly Themes As String() =
            {"light", "dark", "system"}

        Private Shared ReadOnly NotifModes As String() =
            {"all", "important", "none"}

        Private Shared ReadOnly Coupons As String() =
            {"WELCOME10", "FREESHIP", "VIP20", "BR5OFF", "SPRING", "SAVE15", "WELCOME5"}

        Private Shared ReadOnly ShippingMethods As String() =
            {"standard", "express", "pickup"}

        Private Shared ReadOnly Carriers As String() =
            {"correios", "jadlog", "loggi", "dhl", "fedex"}

        Private Shared ReadOnly ShipStatuses As String() =
            {"created", "packed", "shipped", "in_transit", "out_for_delivery", "delivered", "delayed"}

        Private Shared ReadOnly Channels As String() =
            {"web", "mobile", "partner"}

        Private Shared ReadOnly Devices As String() =
            {"android", "ios", "windows", "linux", "mac"}

        Private Shared ReadOnly OSes As String() =
            {"Android 14", "Android 13", "iOS 17", "iOS 16", "Windows 11", "Ubuntu 24.04", "macOS 14"}

        Private Shared ReadOnly Stores As String() =
            {"main", "outlet", "marketplace", "b2b"}

        Private Shared ReadOnly PaymentMethods As String() =
            {"card", "pix", "boleto", "wallet"}

        Private Shared ReadOnly Psps As String() =
            {"stripe", "adyen", "mercadopago", "stone", "cielo"}

        Private Shared ReadOnly RiskBands As String() =
            {"low", "medium", "high"}

        Private Shared ReadOnly ProductNames As String() =
            {"T-Shirt", "Sneakers", "Headphones", "Backpack", "Bottle", "Keyboard", "Mouse", "Monitor", "Book", "Coffee", "Lamp", "Chair"}

        Private Shared ReadOnly Categories As String() =
            {"fashion", "electronics", "home", "sports", "books", "food"}

        Private Shared ReadOnly Brands As String() =
            {"Acme", "North", "Urban", "Soline", "GenericCo", "Prime", "Neo"}

        Private Shared ReadOnly Colors As String() =
            {"black", "white", "blue", "red", "green", "gray"}

        Private Shared ReadOnly Sizes As String() =
            {"xs", "s", "m", "l", "xl", "one"}

        Private Shared ReadOnly Materials As String() =
            {"cotton", "leather", "plastic", "metal", "glass", "paper"}

        Private Shared ReadOnly TagPool As String() =
            {"promo", "new", "hot", "gift", "eco", "premium", "basic", "limited", "sale", "bundle", "cache-me"}

        Private Shared ReadOnly Segments As String() =
            {"new_user", "returning", "vip", "at_risk", "b2b", "student"}

        Private Shared ReadOnly Notes As String() =
            {"gift wrap", "deliver after 18h", "leave at reception", "fragile", "call on arrival", "do not ring bell"}

        Private Shared ReadOnly GiftWrap As String() =
            {"none", "basic", "premium"}

        Private Shared ReadOnly Warehouses As String() =
            {"SP-01", "PE-02", "RJ-03", "MG-01"}

        Private Shared ReadOnly StatesBR As String() =
            {"PE", "SP", "RJ", "MG", "BA", "RS", "CE", "PR", "SC"}

        Private Shared ReadOnly CitiesBR As String() =
            {"Recife", "São Paulo", "Rio de Janeiro", "Belo Horizonte", "Salvador", "Porto Alegre", "Fortaleza", "Curitiba", "Florianópolis"}

        Private Shared ReadOnly Streets As String() =
            {"Av. Central", "Rua das Flores", "Rua do Sol", "Av. Brasil", "Rua A", "Rua B", "Rua C", "Av. Norte", "Rua Sul"}

        Private Shared ReadOnly SourceSystems As String() =
            {"orders-api", "sync-job", "mobile-app", "web-checkout"}

        Private Shared ReadOnly Envs As String() =
            {"prod", "staging"}

        Private Shared ReadOnly Regions As String() =
            {"sa-east-1", "us-east-1", "eu-west-1"}

        Private Shared ReadOnly Pipelines As String() =
            {"ci", "nightly", "release"}

#End Region

    End Class

End Namespace