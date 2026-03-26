Imports System.Globalization
Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Date/time scalar token in the LINQ DOM layer.
    '''
    ''' This type represents a temporal value inside Bytery's JSON-like object model.
    ''' The underlying CLR value is stored as <see cref="Date?"/> so the token can
    ''' represent either a concrete timestamp or a null value.
    '''
    ''' Serialization behavior:
    ''' - <see cref="ToString"/> returns ISO 8601 round-trip text ("o") for non-null values
    ''' - <see cref="ToJsonString"/> emits that same value as a JSON string
    ''' - <see cref="ToObject"/> returns a boxed <see cref="Date"/> or <c>Nothing</c>
    ''' </summary>
    Public NotInheritable Class BDate
        Inherits BValue

        ''' <summary>
        ''' Backing date/time value.
        ''' A missing value is represented by <c>Nothing</c>.
        ''' </summary>
        Private ReadOnly _value As Date

        ''' <summary>
        ''' Creates a date token from a non-null CLR <see cref="Date"/> value.
        ''' </summary>
        Public Sub New(value As Date)
            _value = value
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return JsonFieldType.Date
            End Get
        End Property

        ''' <summary>
        ''' Gets the underlying nullable date/time value.
        ''' </summary>
        Public ReadOnly Property Value As Date
            Get
                Return _value
            End Get
        End Property

        ''' <summary>
        ''' Gets the base token kind for this node.
        ''' Always returns <see cref="Enum_BaseType.Date"/>.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Date
            End Get
        End Property

        ''' <summary>
        ''' Converts the token to the requested CLR target type.
        '''
        ''' Supported conversions are delegated to
        ''' <see cref="BValue.ConvertDateValue(Of T)(Date?, String)"/>.
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T
            Return ConvertDateValue(Of T)(_value, NameOf(BDate))
        End Function

        ''' <summary>
        ''' Converts the token to a plain CLR object.
        '''
        ''' Returns a boxed <see cref="Date"/> for non-null values,
        ''' or <c>Nothing</c> when this token is null.
        ''' </summary>
        Public Overrides Function ToObject() As Object
            Return _value
        End Function

        ''' <summary>
        ''' Returns a textual representation of the token.
        '''
        ''' Output values:
        ''' - ISO 8601 round-trip string for non-null values
        ''' - <c>null</c> for null tokens
        ''' </summary>
        Public Overrides Function ToString() As String
            Return _value.ToString("o", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>
        ''' Serializes the token to JSON text.
        '''
        ''' Date/time values are emitted as JSON strings using the round-trip
        ''' ISO 8601 format specifier ("o"). Null values are emitted as <c>null</c>.
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String
            Return EscapeJsonString(_value.ToString("o", CultureInfo.InvariantCulture))
        End Function

    End Class

End Namespace