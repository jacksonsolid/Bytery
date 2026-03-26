Namespace JSON

    ''' <summary>
    ''' Global schema model used by the protocol before session-specific pointer resolution.
    ''' </summary>
    ''' <remarks>
    ''' Unlike session schemas, this type contains no runtime/session pointer values.
    ''' It describes only the logical structure of the payload:
    '''
    ''' - Object schemas expose an ordered list of <see cref="JsonField"/> entries.
    ''' - Map schemas describe the value type stored for each string key.
    ''' - Array schemas describe the element type stored in the collection.
    '''
    ''' The canonical identity of a schema is stored in <see cref="Key"/>.
    ''' </remarks>
    Friend NotInheritable Class JsonSchema

        ''' <summary>
        ''' High-level shape of a schema entry.
        ''' </summary>
        Public Enum SchemaKind
            ''' <summary>
            ''' Structured object with a fixed ordered field list.
            ''' </summary>
            [Object]

            ''' <summary>
            ''' String-keyed dictionary whose values all share the same declared type.
            ''' </summary>
            Map

            ''' <summary>
            ''' Homogeneous array whose elements all share the same declared type.
            ''' </summary>
            Array
        End Enum

        ''' <summary>
        ''' Canonical schema identity key.
        ''' </summary>
        ''' <remarks>
        ''' This key is used for global caching, deduplication, and cross-schema references.
        ''' </remarks>
        Public ReadOnly Key As String

        ''' <summary>
        ''' Structural kind of this schema.
        ''' </summary>
        Public ReadOnly Kind As SchemaKind

        ''' <summary>
        ''' Ordered field list for object schemas.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful when <see cref="Kind"/> is <see cref="SchemaKind.Object"/>.
        ''' For map and array schemas this array is empty.
        ''' </remarks>
        Public ReadOnly Fields As JsonField()

        ''' <summary>
        ''' Declared map value type.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful when <see cref="Kind"/> is <see cref="SchemaKind.Map"/>.
        ''' </remarks>
        Public ReadOnly DictValueType As JsonFieldType

        ''' <summary>
        ''' Referenced schema key for map values whose base type is Object.
        ''' </summary>
        ''' <remarks>
        ''' Empty or <c>Nothing</c> for non-object map values.
        ''' </remarks>
        Public ReadOnly DictValueSchemaKey As String

        ''' <summary>
        ''' Declared array element type.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful when <see cref="Kind"/> is <see cref="SchemaKind.Array"/>.
        ''' </remarks>
        Public ReadOnly ArrayValueType As JsonFieldType

        ''' <summary>
        ''' Referenced schema key for array elements whose base type is Object.
        ''' </summary>
        ''' <remarks>
        ''' Empty or <c>Nothing</c> for non-object array elements.
        ''' </remarks>
        Public ReadOnly ArrayValueSchemaKey As String

        ''' <summary>
        ''' Creates an object schema.
        ''' </summary>
        ''' <param name="key">Canonical schema identity key.</param>
        ''' <param name="fields">Ordered object field definitions.</param>
        ''' <remarks>
        ''' This constructor initializes:
        '''
        ''' - <see cref="Kind"/> = <see cref="SchemaKind.Object"/>
        ''' - <see cref="Fields"/> = provided field list, or an empty array
        ''' - map/array-specific members = neutral defaults
        ''' </remarks>
        Public Sub New(key As String, fields As JsonField())
            Me.Key = key
            Me.Fields = If(fields, Array.Empty(Of JsonField)())
            Me.Kind = SchemaKind.Object
            Me.DictValueType = JsonFieldType.Unknown
            Me.DictValueSchemaKey = Nothing
        End Sub

        ''' <summary>
        ''' Creates a map or array schema.
        ''' </summary>
        ''' <param name="key">Canonical schema identity key.</param>
        ''' <param name="kind">Schema shape to create. Must be Map or Array.</param>
        ''' <param name="kindValueType">Declared value/element type for the selected kind.</param>
        ''' <param name="kindValueSchemaKey">Referenced schema key for object-like values or elements.</param>
        ''' <remarks>
        ''' Expected layouts:
        '''
        ''' - Map:
        '''   - <see cref="DictValueType"/> is populated
        '''   - <see cref="DictValueSchemaKey"/> may be used when the base type is Object
        '''
        ''' - Array:
        '''   - <see cref="ArrayValueType"/> is populated
        '''   - <see cref="ArrayValueSchemaKey"/> may be used when the base type is Object
        '''
        ''' Passing <see cref="SchemaKind.Object"/> to this constructor is invalid.
        ''' </remarks>
        Public Sub New(key As String, kind As SchemaKind, kindValueType As JsonFieldType, kindValueSchemaKey As String)
            Me.Key = key
            Me.Fields = Array.Empty(Of JsonField)()
            Me.Kind = kind

            Select Case kind
                Case SchemaKind.Map
                    Me.DictValueType = kindValueType
                    Me.DictValueSchemaKey = kindValueSchemaKey

                Case SchemaKind.Array
                    Me.ArrayValueType = kindValueType
                    Me.ArrayValueSchemaKey = kindValueSchemaKey

                Case Else
                    Throw New Exception("Invalid Schema Kind for this signature: " & kind.ToString)
            End Select
        End Sub

    End Class

End Namespace