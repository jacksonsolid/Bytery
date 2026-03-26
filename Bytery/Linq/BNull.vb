Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Singleton token that represents the JSON null literal in the LINQ DOM layer.
    '''
    ''' This type is used whenever Bytery needs a canonical in-memory representation
    ''' of a missing or null value. The class exposes a single shared instance so
    ''' null tokens do not need to be allocated repeatedly.
    ''' </summary>
    Public NotInheritable Class BNull
        Inherits BValue

        ''' <summary>
        ''' Shared singleton instance used across the entire object model.
        ''' </summary>
        Private Shared ReadOnly _Instances As New Dictionary(Of JsonFieldType, BNull) From {
            {JsonFieldType.Unknown, New BNull(JsonFieldType.Unknown)},
            {JsonFieldType.Integer, New BNull(JsonFieldType.Integer)},
            {JsonFieldType.Integer Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Integer Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.Float4Bytes, New BNull(JsonFieldType.Float4Bytes)},
            {JsonFieldType.Float4Bytes Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Float4Bytes Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.Float8Bytes, New BNull(JsonFieldType.Float8Bytes)},
            {JsonFieldType.Float8Bytes Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Float8Bytes Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.Boolean, New BNull(JsonFieldType.Boolean)},
            {JsonFieldType.Boolean Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Boolean Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.Date, New BNull(JsonFieldType.Date)},
            {JsonFieldType.Date Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Date Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.String, New BNull(JsonFieldType.String)},
            {JsonFieldType.String Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.String Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.Bytes, New BNull(JsonFieldType.Bytes)},
            {JsonFieldType.Bytes Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Bytes Or JsonFieldType.ArrayFlag)},
            {JsonFieldType.Object, New BNull(JsonFieldType.Object)},
            {JsonFieldType.Object Or JsonFieldType.ArrayFlag, New BNull(JsonFieldType.Object Or JsonFieldType.ArrayFlag)}
        }

        Private ReadOnly _fieldCode As JsonFieldType

        Public Shared Function Instance(code As JsonFieldType) As BNull
            Dim result As BNull = Nothing
            If _Instances.TryGetValue(code, result) Then Return result
            Throw New KeyNotFoundException("Unsupported BNull field code: " & code.ToString())
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return Object.ReferenceEquals(Me, obj)
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return CInt(_fieldCode)
        End Function

        Private Sub New(fieldCode As JSON.JsonFieldType)
            Me._fieldCode = fieldCode
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return _fieldCode
            End Get
        End Property

        ''' <summary>
        ''' Gets the base token kind for this node.
        ''' Always returns <see cref="Enum_BaseType.Null"/>.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Null
            End Get
        End Property

        ''' <summary>
        ''' Gets whether this token represents a null value.
        ''' Always returns <c>True</c>.
        ''' </summary>
        Public Overrides ReadOnly Property IsNull As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Converts this null token to the requested CLR type.
        '''
        ''' Reference types and nullable value types return <c>Nothing</c>.
        ''' Non-nullable value types throw, because JSON null cannot be converted
        ''' to a required CLR value.
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T
            Return ConvertNullValue(Of T)(NameOf(BNull))
        End Function

        ''' <summary>
        ''' Converts the token to its plain CLR representation.
        ''' Always returns <c>Nothing</c>.
        ''' </summary>
        Public Overrides Function ToObject() As Object
            Return Nothing
        End Function

        ''' <summary>
        ''' Returns the textual representation of the token.
        ''' Always returns the JSON literal <c>null</c>.
        ''' </summary>
        Public Overrides Function ToString() As String
            Return "null"
        End Function

        ''' <summary>
        ''' Serializes the token as JSON text.
        ''' Indentation parameters are ignored because null is a scalar literal.
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String
            Return "null"
        End Function

    End Class

End Namespace