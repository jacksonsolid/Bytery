Imports Bytery.JSON

Namespace DotNet

    ''' <summary>
    ''' Reflection-side metadata for a single object field/property included in a <see cref="DotNetSchema"/>.
    ''' </summary>
    ''' <remarks>
    ''' This type keeps both schema information and runtime access information:
    '''
    ''' - <see cref="Name"/> is the public member name exposed by the schema.
    ''' - <see cref="TypeCode"/> is the wire-facing field type classification.
    ''' - <see cref="RefSchemaKey"/> is used only when the base type is Object.
    ''' - <see cref="Getter"/> reads the member value from a boxed instance.
    ''' - <see cref="RefDotNetType"/> points to the referenced .NET type when the base type is Object.
    ''' </remarks>
    Friend NotInheritable Class DotNetFieldMeta

        ''' <summary>
        ''' Public schema name of the member.
        ''' </summary>
        Public ReadOnly Name As String

        ''' <summary>
        ''' Wire-facing field type assigned to the member.
        ''' </summary>
        Public ReadOnly TypeCode As JsonFieldType

        ''' <summary>
        ''' Referenced schema key for object-like members.
        ''' </summary>
        ''' <remarks>
        ''' Empty for non-object base types.
        ''' </remarks>
        Public ReadOnly RefSchemaKey As String

        ''' <summary>
        ''' Compiled getter used to read the member value from a boxed object instance.
        ''' </summary>
        Public ReadOnly Getter As Func(Of Object, Object)

        ''' <summary>
        ''' Referenced .NET type for object-like members.
        ''' </summary>
        ''' <remarks>
        ''' This is only meaningful when the base type of <see cref="TypeCode"/> is Object.
        ''' </remarks>
        Public ReadOnly RefDotNetType As Type

        ''' <summary>
        ''' Creates a new field metadata record.
        ''' </summary>
        ''' <param name="name">Public schema name of the member.</param>
        ''' <param name="typeCode">Wire-facing field type classification.</param>
        ''' <param name="refSchemaKey">Referenced schema key for object-like members.</param>
        ''' <param name="getter">Compiled getter delegate for the member.</param>
        ''' <param name="refDotNetType">Referenced .NET type for object-like members.</param>
        ''' <remarks>
        ''' String inputs are normalized to empty strings when <c>Nothing</c> is provided.
        ''' </remarks>
        Public Sub New(name As String,
                       typeCode As JsonFieldType,
                       refSchemaKey As String,
                       getter As Func(Of Object, Object),
                       refDotNetType As Type)

            Me.Name = If(name, "")
            Me.TypeCode = typeCode
            Me.RefSchemaKey = If(refSchemaKey, "")
            Me.Getter = getter
            Me.RefDotNetType = refDotNetType
        End Sub
    End Class

    ''' <summary>
    ''' Reflection-oriented schema model for a .NET type.
    ''' </summary>
    ''' <remarks>
    ''' A <see cref="DotNetSchema"/> represents one of three shapes:
    '''
    ''' - Object
    ''' - Map
    ''' - Array
    '''
    ''' The shape is defined by <see cref="Kind"/>. Only the properties relevant to the
    ''' selected kind are populated.
    '''
    ''' This model is richer than the final wire-facing <see cref="JsonSchema"/> because it
    ''' also preserves runtime information such as <see cref="DotNetType"/> and
    ''' <see cref="DotNetFieldMeta.Getter"/>.
    ''' </remarks>
    Friend NotInheritable Class DotNetSchema

        ''' <summary>
        ''' Original .NET type that produced this schema.
        ''' </summary>
        Public ReadOnly DotNetType As Type

        ''' <summary>
        ''' Structural kind of the schema.
        ''' </summary>
        Public ReadOnly Kind As JsonSchema.SchemaKind

        ''' <summary>
        ''' Indicates whether this schema represents a map.
        ''' </summary>
        Public ReadOnly IsMap As Boolean

        ''' <summary>
        ''' Indicates whether this schema represents an array.
        ''' </summary>
        Public ReadOnly IsArray As Boolean

        ''' <summary>
        ''' Ordered object-field metadata for object schemas.
        ''' </summary>
        ''' <remarks>
        ''' Empty for map and array schemas.
        ''' </remarks>
        Public ReadOnly FieldsMeta As DotNetFieldMeta()

        ''' <summary>
        ''' Wire-facing value type for map schemas.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful when <see cref="Kind"/> is <see cref="JsonSchema.SchemaKind.Map"/>.
        ''' </remarks>
        Public ReadOnly MapValueTypeCode As JsonFieldType

        ''' <summary>
        ''' Referenced schema key for object-like map values.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful for map schemas whose base value type is Object.
        ''' </remarks>
        Public ReadOnly MapValueSchemaKey As String

        ''' <summary>
        ''' Wire-facing element type for array schemas.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful when <see cref="Kind"/> is <see cref="JsonSchema.SchemaKind.Array"/>.
        ''' </remarks>
        Public ReadOnly ArrayValueTypeCode As JsonFieldType

        ''' <summary>
        ''' Referenced schema key for object-like array elements.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful for array schemas whose element base type is Object.
        ''' </remarks>
        Public ReadOnly ArrayValueSchemaKey As String

        ''' <summary>
        ''' Canonical schema signature key used for caching and schema identity.
        ''' </summary>
        Public ReadOnly JsonSchemaKey As String

        ''' <summary>
        ''' Creates an object schema.
        ''' </summary>
        ''' <param name="dotNetType">Original .NET type represented by the schema.</param>
        ''' <param name="jsonSchemaKey">Canonical schema signature key.</param>
        ''' <param name="fieldsMeta">Ordered field metadata for the object schema.</param>
        ''' <remarks>
        ''' This constructor produces a schema with:
        '''
        ''' - <see cref="Kind"/> = Object
        ''' - <see cref="IsMap"/> = False
        ''' - <see cref="IsArray"/> = False
        '''
        ''' Map and array-specific members are initialized to neutral defaults.
        ''' </remarks>
        Public Sub New(dotNetType As Type,
                       jsonSchemaKey As String,
                       fieldsMeta As DotNetFieldMeta())

            Me.DotNetType = dotNetType
            Me.JsonSchemaKey = If(jsonSchemaKey, "")
            Me.Kind = JsonSchema.SchemaKind.Object

            Me.IsMap = False
            Me.IsArray = False

            Me.FieldsMeta = If(fieldsMeta, Array.Empty(Of DotNetFieldMeta)())

            Me.MapValueTypeCode = JsonFieldType.Unknown
            Me.MapValueSchemaKey = ""

            Me.ArrayValueTypeCode = JsonFieldType.Unknown
            Me.ArrayValueSchemaKey = ""
        End Sub

        ''' <summary>
        ''' Creates a map schema.
        ''' </summary>
        ''' <param name="dotNetType">Original .NET type represented by the schema.</param>
        ''' <param name="jsonSchemaKey">Canonical schema signature key.</param>
        ''' <param name="mapValueTypeCode">Wire-facing value type for the map.</param>
        ''' <param name="mapValueSchemaKey">Referenced schema key for object-like map values.</param>
        ''' <remarks>
        ''' This overload is a convenience wrapper that forwards to the generalized
        ''' kind-based constructor using <see cref="JsonSchema.SchemaKind.Map"/>.
        ''' </remarks>
        Public Sub New(dotNetType As Type,
                       jsonSchemaKey As String,
                       mapValueTypeCode As JsonFieldType,
                       mapValueSchemaKey As String)

            Me.New(dotNetType,
                   jsonSchemaKey,
                   JsonSchema.SchemaKind.Map,
                   mapValueTypeCode,
                   mapValueSchemaKey)
        End Sub

        ''' <summary>
        ''' Creates a non-object schema whose shape is determined by <paramref name="kind"/>.
        ''' </summary>
        ''' <param name="dotNetType">Original .NET type represented by the schema.</param>
        ''' <param name="jsonSchemaKey">Canonical schema signature key.</param>
        ''' <param name="kind">Schema kind to construct.</param>
        ''' <param name="kindValueTypeCode">Map value type or array element type.</param>
        ''' <param name="kindValueSchemaKey">Referenced schema key for object-like values/elements.</param>
        ''' <remarks>
        ''' Supported kinds here are:
        '''
        ''' - <see cref="JsonSchema.SchemaKind.Map"/>
        ''' - <see cref="JsonSchema.SchemaKind.Array"/>
        '''
        ''' Object schemas use the dedicated object constructor instead.
        ''' </remarks>
        Public Sub New(dotNetType As Type,
                       jsonSchemaKey As String,
                       kind As JsonSchema.SchemaKind,
                       kindValueTypeCode As JsonFieldType,
                       kindValueSchemaKey As String)

            Me.DotNetType = dotNetType
            Me.JsonSchemaKey = If(jsonSchemaKey, "")
            Me.FieldsMeta = Array.Empty(Of DotNetFieldMeta)()

            Me.Kind = kind

            Select Case kind

                Case JsonSchema.SchemaKind.Map
                    Me.IsMap = True
                    Me.IsArray = False

                    Me.MapValueTypeCode = kindValueTypeCode
                    Me.MapValueSchemaKey = If(kindValueSchemaKey, "")

                    Me.ArrayValueTypeCode = JsonFieldType.Unknown
                    Me.ArrayValueSchemaKey = ""

                Case JsonSchema.SchemaKind.Array
                    Me.IsMap = False
                    Me.IsArray = True

                    Me.ArrayValueTypeCode = kindValueTypeCode
                    Me.ArrayValueSchemaKey = If(kindValueSchemaKey, "")

                    Me.MapValueTypeCode = JsonFieldType.Unknown
                    Me.MapValueSchemaKey = ""

                Case Else
                    Throw New Exception("Invalid SchemaKind for kinded ctor: " & kind.ToString())

            End Select

        End Sub

    End Class

End Namespace