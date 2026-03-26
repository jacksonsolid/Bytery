Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' String scalar token in the LINQ DOM layer.
    '''
    ''' This type wraps a CLR <see cref="String"/> while preserving the JSON-like
    ''' token abstraction used across the LINQ namespace. A null reference represents
    ''' a JSON null string value.
    ''' </summary>
    Public NotInheritable Class BString
        Inherits BValue

        ''' <summary>
        ''' Backing storage for the wrapped string value.
        ''' A null reference means this token represents null.
        ''' </summary>
        Private ReadOnly _value As String

        ''' <summary>
        ''' Initializes a new string token.
        ''' Pass <c>Nothing</c> to create a null string token.
        ''' </summary>
        Public Sub New(value As String)
            _value = value
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return JsonFieldType.String
            End Get
        End Property

        ''' <summary>
        ''' Gets the wrapped string value.
        ''' Returns <c>Nothing</c> when this token represents null.
        ''' </summary>
        Public ReadOnly Property Value As String
            Get
                Return _value
            End Get
        End Property

        ''' <summary>
        ''' Gets the base token kind for this node.
        ''' Always returns <see cref="Enum_BaseType.String"/>.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.String
            End Get
        End Property

        ''' <summary>
        ''' Converts the wrapped string to the requested CLR type.
        '''
        ''' Conversion rules are centralized in <c>ConvertStringValue</c>, which keeps
        ''' string conversions consistent across the LINQ token hierarchy.
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T
            Return ConvertStringValue(Of T)(_value, NameOf(BString))
        End Function

        ''' <summary>
        ''' Converts the token to its plain CLR representation.
        ''' Returns the underlying string reference, which may be <c>Nothing</c>.
        ''' </summary>
        Public Overrides Function ToObject() As Object
            Return _value
        End Function

        ''' <summary>
        ''' Returns a human-readable representation of the token.
        ''' For null values, returns the literal text <c>"null"</c>.
        ''' </summary>
        Public Overrides Function ToString() As String
            Return _value
        End Function

        ''' <summary>
        ''' Serializes the token as a JSON string literal.
        '''
        ''' String escaping is delegated to <c>EscapeJsonString</c> so control characters,
        ''' quotes, backslashes, and null values are encoded consistently.
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String
            Return EscapeJsonString(_value)
        End Function

    End Class

End Namespace