Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Byte-array scalar token in the LINQ DOM layer.
    '''
    ''' This type represents binary payloads inside the JSON-like abstraction used by
    ''' Bytery. The underlying CLR value is stored as a <see cref="Byte()"/> and may
    ''' also be <c>Nothing</c> to represent a null token.
    '''
    ''' Serialization behavior:
    ''' - <see cref="ToString"/> returns Base64 text for non-null values
    ''' - <see cref="ToJsonString"/> emits a JSON string containing that Base64 text
    ''' - <see cref="ToObject"/> returns the raw <see cref="Byte()"/> reference
    ''' </summary>
    Public NotInheritable Class BBytes
        Inherits BValue

        ''' <summary>
        ''' Backing binary value.
        ''' A <c>Nothing</c> reference represents a null token.
        ''' </summary>
        Private ReadOnly _value As Byte()

        ''' <summary>
        ''' Creates a byte-array token from the supplied binary value.
        ''' Pass <c>Nothing</c> to create a null token.
        ''' </summary>
        Public Sub New(value As Byte())
            _value = value
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return JsonFieldType.Bytes
            End Get
        End Property

        ''' <summary>
        ''' Gets the underlying byte-array value.
        ''' May be <c>Nothing</c> when this token represents null.
        ''' </summary>
        Public ReadOnly Property Value As Byte()
            Get
                Return _value
            End Get
        End Property

        ''' <summary>
        ''' Gets the base token kind for this node.
        ''' Always returns <see cref="Enum_BaseType.Byte"/>.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Byte
            End Get
        End Property

        ''' <summary>
        ''' Converts the token to the requested CLR target type.
        '''
        ''' Supported conversions are defined by <see cref="BValue.ConvertBytesValue(Of T)"/>.
        ''' This typically includes <see cref="Byte()"/>, <see cref="Object"/>, and other
        ''' compatible targets handled by the shared conversion layer.
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T
            Return ConvertBytesValue(Of T)(_value, NameOf(BBytes))
        End Function

        ''' <summary>
        ''' Converts the token to a plain CLR object.
        '''
        ''' Returns the raw <see cref="Byte()"/> reference, or <c>Nothing</c> when null.
        ''' </summary>
        Public Overrides Function ToObject() As Object
            Return _value
        End Function

        ''' <summary>
        ''' Returns a textual representation of the token.
        '''
        ''' Output values:
        ''' - Base64 string for non-null binary payloads
        ''' - <c>null</c> for null tokens
        ''' </summary>
        Public Overrides Function ToString() As String
            If _value Is Nothing Then Return "null"
            Return Convert.ToBase64String(_value)
        End Function

        ''' <summary>
        ''' Serializes the token to JSON text.
        '''
        ''' Binary values are rendered as JSON strings containing Base64 text.
        ''' Null values are rendered as the JSON literal <c>null</c>.
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String
            If _value Is Nothing Then Return "null"
            Return EscapeJsonString(Convert.ToBase64String(_value))
        End Function

    End Class

End Namespace