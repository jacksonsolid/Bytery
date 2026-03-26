Imports System.Globalization
Imports Bytery.JSON

Namespace Linq

    Public NotInheritable Class BNumber
        Inherits BValue

        Private ReadOnly _fieldCode As JSON.JsonFieldType
        Private ReadOnly _i64 As Long
        Private ReadOnly _f32 As Single
        Private ReadOnly _f64 As Double

        Public Sub New(value As Long)
            _fieldCode = JSON.JsonFieldType.Integer
            _i64 = value
        End Sub

        Public Sub New(value As Single)
            _fieldCode = JSON.JsonFieldType.Float4Bytes
            _f32 = value
        End Sub

        Public Sub New(value As Double)
            _fieldCode = JSON.JsonFieldType.Float8Bytes
            _f64 = value
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return Me._fieldcode
            End Get
        End Property

        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Number
            End Get
        End Property

        Public ReadOnly Property Value As Double
            Get
                Select Case _fieldCode
                    Case JSON.JsonFieldType.Integer : Return _i64
                    Case JSON.JsonFieldType.Float4Bytes : Return _f32
                    Case JSON.JsonFieldType.Float8Bytes : Return _f64
                    Case Else : Throw New InvalidCastException("Invalid numeric kind in BNumber: " & _fieldCode.ToString())
                End Select
            End Get
        End Property

        Public Overrides Function GetValue(Of T)() As T

            Select Case _fieldCode
                Case JSON.JsonFieldType.Integer
                    Return ConvertNumberValue(Of T)(_i64, NameOf(BNumber))

                Case JSON.JsonFieldType.Float4Bytes
                    Return ConvertNumberValue(Of T)(_f32, NameOf(BNumber))

                Case JSON.JsonFieldType.Float8Bytes
                    Return ConvertNumberValue(Of T)(_f64, NameOf(BNumber))

                Case Else
                    Throw New InvalidCastException("Invalid numeric kind in BNumber: " & _fieldCode.ToString())
            End Select
        End Function

        Public Overrides Function ToObject() As Object

            Select Case _fieldCode
                Case JSON.JsonFieldType.Integer
                    Return _i64

                Case JSON.JsonFieldType.Float4Bytes
                    Return _f32

                Case JSON.JsonFieldType.Float8Bytes
                    Return _f64

                Case Else
                    Throw New InvalidOperationException("Invalid numeric kind in BNumber: " & _fieldCode.ToString())
            End Select
        End Function

        Public Overrides Function ToString() As String

            Select Case _fieldCode
                Case JSON.JsonFieldType.Integer
                    Return _i64.ToString(CultureInfo.InvariantCulture)

                Case JSON.JsonFieldType.Float4Bytes
                    Return _f32.ToString("R", CultureInfo.InvariantCulture)

                Case JSON.JsonFieldType.Float8Bytes
                    Return _f64.ToString("R", CultureInfo.InvariantCulture)

                Case Else
                    Throw New InvalidOperationException("Invalid numeric kind in BNumber: " & _fieldCode.ToString())
            End Select
        End Function

        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String
            Return Me.ToString()
        End Function

    End Class

End Namespace