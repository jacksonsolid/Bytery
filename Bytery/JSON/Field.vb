Namespace JSON

    ''' <summary>
    ''' Wire-facing field type identifiers used by schemas and data slots.
    ''' </summary>
    ''' <remarks>
    ''' Base type range:
    '''
    '''   0   = Unknown
    '''   1   = Integer
    '''   2   = Float4Bytes
    '''   3   = Float8Bytes
    '''   4   = Boolean
    '''   5   = Date
    '''   6   = String
    '''   7   = Bytes
    '''   8   = Object
    '''
    ''' Array types are represented by OR-ing the base type with <see cref="ArrayFlag"/>.
    '''
    ''' Examples:
    '''   Integer                  = 1
    '''   Integer Or ArrayFlag     = 129
    '''   Object Or ArrayFlag      = 136
    ''' </remarks>
    Public Enum JsonFieldType As Byte
        Unknown = 0
        [Integer] = 1
        Float4Bytes = 2
        Float8Bytes = 3
        [Boolean] = 4
        [Date] = 5
        [String] = 6
        [Bytes] = 7
        [Object] = 8
        ArrayFlag = &H80
    End Enum

    ''' <summary>
    ''' Schema field descriptor used by the JSON-side schema model.
    ''' </summary>
    ''' <remarks>
    ''' This is the protocol-facing version of a field definition.
    '''
    ''' - <see cref="Name"/> is the serialized field name.
    ''' - <see cref="TypeCode"/> is the wire-facing field type.
    ''' - <see cref="RefSchemaKey"/> is only used when the base type is Object.
    '''
    ''' For non-object base types, <see cref="RefSchemaKey"/> should remain empty or <c>Nothing</c>.
    ''' </remarks>
    Friend NotInheritable Class JsonField

        ''' <summary>
        ''' Serialized field name.
        ''' </summary>
        Public Name As String

        ''' <summary>
        ''' Wire-facing field type, optionally including <see cref="JsonFieldType.ArrayFlag"/>.
        ''' </summary>
        Public TypeCode As JsonFieldType

        ''' <summary>
        ''' Referenced schema key for object-like fields.
        ''' </summary>
        ''' <remarks>
        ''' This member is only meaningful when the base type of <see cref="TypeCode"/> is Object,
        ''' including Object arrays.
        ''' </remarks>
        Public RefSchemaKey As String

#Region "FieldType helpers"

        ''' <summary>
        ''' Returns whether a type code represents a primitive scalar value.
        ''' </summary>
        ''' <param name="t">The field type to inspect.</param>
        ''' <returns>
        ''' <c>True</c> when the type is a non-array primitive scalar in the range Integer..String;
        ''' otherwise <c>False</c>.
        ''' </returns>
        ''' <remarks>
        ''' This helper intentionally excludes:
        ''' - Unknown
        ''' - Bytes
        ''' - Object
        ''' - any type with <see cref="JsonFieldType.ArrayFlag"/>
        ''' </remarks>
        Public Shared Function FieldTypeIsPrimitiveAndScalar(t As JsonFieldType) As Boolean
            Return t >= JsonFieldType.Integer AndAlso t <= JsonFieldType.String
        End Function

        ''' <summary>
        ''' Returns whether the base type is Object, regardless of array flag.
        ''' </summary>
        ''' <param name="t">The field type to inspect.</param>
        ''' <returns>
        ''' <c>True</c> for Object and Object-array type codes; otherwise <c>False</c>.
        ''' </returns>
        Public Shared Function FieldTypeIsObjectOrObjectArray(t As JsonFieldType) As Boolean
            Return FieldTypeWithoutArrayFlag(t) = JsonFieldType.Object
        End Function

        ''' <summary>
        ''' Returns whether the array bit is set on the supplied type code.
        ''' </summary>
        ''' <param name="t">The field type to inspect.</param>
        ''' <returns><c>True</c> when <see cref="JsonFieldType.ArrayFlag"/> is present; otherwise <c>False</c>.</returns>
        Public Shared Function FieldTypeIsArray(t As JsonFieldType) As Boolean
            Return (t And JsonFieldType.ArrayFlag) = JsonFieldType.ArrayFlag
        End Function

        ''' <summary>
        ''' Removes the array bit from a field type and returns the base type.
        ''' </summary>
        ''' <param name="t">The field type to normalize.</param>
        ''' <returns>The base scalar/object type with <see cref="JsonFieldType.ArrayFlag"/> cleared.</returns>
        ''' <remarks>
        ''' Examples:
        '''   Integer                  -> Integer
        '''   Integer Or ArrayFlag     -> Integer
        '''   Object Or ArrayFlag      -> Object
        ''' </remarks>
        Public Shared Function FieldTypeWithoutArrayFlag(t As JsonFieldType) As JsonFieldType
            Return t And Not JsonFieldType.ArrayFlag
        End Function

#End Region

    End Class

End Namespace