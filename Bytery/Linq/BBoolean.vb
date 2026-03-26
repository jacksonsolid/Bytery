Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Boolean scalar token in the LINQ DOM layer.
    '''
    ''' This type wraps a nullable Boolean value so the DOM can represent both:
    ''' - a concrete Boolean value (<c>True</c> / <c>False</c>)
    ''' - a JSON-like null
    '''
    ''' Conversion behavior:
    ''' - <see cref="GetValue(Of T)"/> delegates to the shared Boolean conversion
    '''   logic implemented in <see cref="BValue"/>
    ''' - <see cref="ToObject"/> returns either a CLR <see cref="Boolean"/> or <c>Nothing</c>
    ''' - JSON rendering uses the canonical lowercase literals: <c>true</c>, <c>false</c>, <c>null</c>
    ''' </summary>
    Public NotInheritable Class BBoolean
        Inherits BValue

        ''' <summary>
        ''' Backing nullable Boolean value.
        ''' A missing value represents a JSON-like null.
        ''' </summary>
        Private ReadOnly _value As Boolean

        ''' <summary>
        ''' Creates a Boolean token from a non-null Boolean value.
        ''' </summary>
        Public Sub New(value As Boolean)
            _value = value
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return JsonFieldType.Boolean
            End Get
        End Property

        ''' <summary>
        ''' Gets the underlying nullable Boolean value.
        ''' </summary>
        Public ReadOnly Property Value As Boolean?
            Get
                Return _value
            End Get
        End Property

        ''' <summary>
        ''' Gets the base token kind for this node.
        ''' Always returns <see cref="Enum_BaseType.Boolean"/>.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Boolean
            End Get
        End Property

        ''' <summary>
        ''' Converts the token to the requested CLR target type.
        '''
        ''' Supported conversions are defined by <see cref="BValue.ConvertBooleanValue(Of T)"/>.
        ''' If this token is null, nullable/reference targets can receive <c>Nothing</c>,
        ''' while incompatible value types will raise an exception.
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T
            Return ConvertBooleanValue(Of T)(_value, NameOf(BBoolean))
        End Function

        ''' <summary>
        ''' Converts the token to a plain CLR object.
        '''
        ''' Returns:
        ''' - <see cref="Boolean"/> when a value is present
        ''' - <c>Nothing</c> when the token is null
        ''' </summary>
        Public Overrides Function ToObject() As Object
            Return _value
        End Function

        ''' <summary>
        ''' Returns the canonical JSON text form of the Boolean token.
        '''
        ''' Output values:
        ''' - <c>true</c>
        ''' - <c>false</c>
        ''' - <c>null</c>
        ''' </summary>
        Public Overrides Function ToString() As String
            Return If(_value, "true", "false")
        End Function

        ''' <summary>
        ''' Serializes the token to JSON text.
        '''
        ''' Indentation parameters are ignored because Boolean literals are scalar values
        ''' and always render as a single JSON token.
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String
            Return Me.ToString()
        End Function

    End Class

End Namespace