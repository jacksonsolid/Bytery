Imports System.Linq.Expressions
Imports System.Reflection
Imports System.Text
Imports Bytery.JSON

Namespace DotNet

    ''' <summary>
    ''' Builds reflection-based schema descriptions for .NET types.
    ''' </summary>
    ''' <remarks>
    ''' This type is responsible for two related jobs:
    '''
    ''' 1. Build a <see cref="DotNetSchema"/> from a runtime <see cref="Type"/>.
    ''' 2. Convert a <see cref="DotNetSchema"/> into a wire-oriented <see cref="JsonSchema"/>.
    '''
    ''' The builder understands three structural families:
    '''   - Object schemas
    '''   - Map schemas
    '''   - Array schemas
    '''
    ''' Primitive-like types are intentionally excluded because they do not need
    ''' a dedicated schema entry in the protocol.
    ''' </remarks>
    Friend NotInheritable Class Builder

        ''' <summary>
        ''' Static-only type.
        ''' </summary>
        Private Sub New()
        End Sub

        ' Tracks types currently being expanded on the active thread so that
        ' recursive schema-key generation can stop cleanly on cycles.
        <ThreadStatic>
        Private Shared _building As HashSet(Of Type)

        ''' <summary>
        ''' Returns whether the specified type is currently being expanded on this thread.
        ''' </summary>
        ''' <param name="t">The type being inspected.</param>
        Private Shared Function IsBuilding(t As Type) As Boolean
            If _building Is Nothing Then Return False
            Return _building.Contains(t)
        End Function

        ''' <summary>
        ''' Marks a type as currently being expanded on this thread.
        ''' </summary>
        ''' <param name="t">The type being inspected.</param>
        Private Shared Sub PushBuilding(t As Type)
            If _building Is Nothing Then _building = New HashSet(Of Type)()
            _building.Add(t)
        End Sub

        ''' <summary>
        ''' Removes a type from the active expansion set on this thread.
        ''' </summary>
        ''' <param name="t">The type that finished expansion.</param>
        Private Shared Sub PopBuilding(t As Type)
            If _building Is Nothing Then Return
            _building.Remove(t)
        End Sub

        ''' <summary>
        ''' Builds a <see cref="DotNetSchema"/> for a .NET type.
        ''' </summary>
        ''' <param name="t">The runtime type to analyze.</param>
        ''' <returns>The schema that describes the type.</returns>
        ''' <remarks>
        ''' Dispatch rules:
        '''   - dictionary types produce a map schema
        '''   - array/enumerable types produce an array schema
        '''   - all remaining supported reference/object types produce an object schema
        '''
        ''' Primitive-like types are rejected because they are encoded directly as values.
        '''
        ''' Recursive references are cycle-guarded. When a type is encountered again
        ''' while its key is still being built, a temporary minimal schema using the
        ''' placeholder key <c>"@cycle"</c> is returned to break the recursion.
        ''' </remarks>
        Public Shared Function BuildDotNetSchema(t As Type) As DotNetSchema
            If t Is Nothing Then Throw New ArgumentNullException(NameOf(t))

            If IsBuilding(t) Then
                Return New DotNetSchema(t, "@cycle", Array.Empty(Of DotNetFieldMeta)())
            End If

            PushBuilding(t)
            Try
                Dim info = Cache.GetInfo(t)
                If info Is Nothing Then Throw New Exception("Missing TypeInfo for: " & t.FullName)

                If info.IsPrimitiveLike Then
                    Throw New ArgumentException($"Primitive-like types do not have a DotNetSchema: {t.FullName}", NameOf(t))
                End If

                If info.IsDictionary Then
                    Return BuildMapSchema(t, info)
                End If

                If t.IsArray OrElse info.EnumerableElement IsNot Nothing Then
                    Return BuildArraySchema(t, info)
                End If

                Return BuildObjectSchema(t)

            Finally
                PopBuilding(t)
            End Try
        End Function

        ''' <summary>
        ''' Builds an array schema from a .NET array or enumerable type.
        ''' </summary>
        ''' <param name="t">The container type.</param>
        ''' <param name="info">Cached type classification for <paramref name="t"/>.</param>
        ''' <returns>A <see cref="DotNetSchema"/> whose kind is <c>Array</c>.</returns>
        ''' <remarks>
        ''' The schema stores the logical element type used by the wire protocol.
        '''
        ''' Special handling:
        '''   - If the element itself is an array/enumerable, it is represented as
        '''     <c>Array&lt;Object&gt;</c> with a referenced schema key for that nested array shape.
        '''   - If the element base type is Object, the referenced object schema key is attached.
        ''' </remarks>
        Private Shared Function BuildArraySchema(t As Type, info As Cache.TypeInfo) As DotNetSchema

            Dim elemDeclared As Type =
        If(t.IsArray, t.GetElementType(), info.EnumerableElement)

            If elemDeclared Is Nothing Then
                Throw New Exception("Array schema missing element type: " & t.FullName)
            End If

            Dim elemTc As JsonFieldType = Cache.TypeToFieldType(elemDeclared)

            Dim elemIsArr As Boolean = JsonField.FieldTypeIsArray(elemTc)
            Dim elemBase As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(elemTc)

            Dim schemaElemType As JsonFieldType
            Dim schemaElemKey As String = ""

            If elemIsArr Then
                schemaElemType = JsonFieldType.Object
                schemaElemKey = Cache.GetJsonSchemaKey(elemDeclared)
            Else
                schemaElemType = elemBase
                If elemBase = JsonFieldType.Object Then
                    Dim refType As Type = ResolveObjectSchemaType(elemDeclared)
                    schemaElemKey = GetRefSchemaKey(refType)
                End If
            End If

            Dim key As String = BuildArrayKey(schemaElemType, schemaElemKey)
            Return New DotNetSchema(t, key, JsonSchema.SchemaKind.Array, schemaElemType, schemaElemKey)

        End Function

        ''' <summary>
        ''' Builds the canonical signature key for an array schema.
        ''' </summary>
        ''' <param name="elemType">The protocol element type stored in the array schema.</param>
        ''' <param name="elemSchemaKey">The referenced schema key for object-like elements, when applicable.</param>
        ''' <returns>A stable string key used for schema identity and caching.</returns>
        Private Shared Function BuildArrayKey(elemType As JsonFieldType, elemSchemaKey As String) As String
            Dim sb As New StringBuilder(64)
            sb.Append("arr-")
            sb.Append(CInt(elemType))
            sb.Append("-"c)
            If Not String.IsNullOrEmpty(elemSchemaKey) Then
                sb.Append(elemSchemaKey)
                sb.Append("-"c)
            End If
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Builds a map schema from a supported generic dictionary type.
        ''' </summary>
        ''' <param name="t">The dictionary type.</param>
        ''' <param name="info">Cached type classification for <paramref name="t"/>.</param>
        ''' <returns>A <see cref="DotNetSchema"/> whose kind is <c>Map</c>.</returns>
        ''' <remarks>
        ''' Only generic dictionary forms are supported here because the value type
        ''' must be known in order to build a deterministic schema.
        ''' </remarks>
        Private Shared Function BuildMapSchema(t As Type, info As Cache.TypeInfo) As DotNetSchema

            If info.DictValue Is Nothing Then
                Throw New NotSupportedException($"Non-generic dictionaries are not supported in schema generation: {t.FullName}")
            End If

            Dim valType As Type = info.DictValue
            Dim valTypeCode As JsonFieldType = Cache.TypeToFieldType(valType)
            Dim valBase As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(valTypeCode)

            Dim valSchemaKey As String = ""
            If valBase = JsonFieldType.Object Then
                Dim refType As Type = ResolveObjectSchemaType(valType)
                valSchemaKey = GetRefSchemaKey(refType)
            End If

            Dim key As String = BuildMapKey(valTypeCode, valSchemaKey)
            Return New DotNetSchema(t, key, valTypeCode, valSchemaKey)
        End Function

        ''' <summary>
        ''' Builds the canonical signature key for a map schema.
        ''' </summary>
        ''' <param name="valTypeCode">The full map value type code.</param>
        ''' <param name="valSchemaKey">The referenced schema key for object-like map values, when applicable.</param>
        ''' <returns>A stable string key used for schema identity and caching.</returns>
        Private Shared Function BuildMapKey(valTypeCode As JsonFieldType, valSchemaKey As String) As String
            Dim sb As New StringBuilder(64)
            sb.Append("map-")
            sb.Append(CInt(valTypeCode))
            sb.Append("-"c)
            If Not String.IsNullOrEmpty(valSchemaKey) Then
                sb.Append(valSchemaKey)
                sb.Append("-"c)
            End If
            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Temporary reflection metadata used while building an object schema.
        ''' </summary>
        Private NotInheritable Class MemberMeta
            Public ReadOnly Name As String
            Public ReadOnly DeclaredType As Type
            Public ReadOnly Getter As Func(Of Object, Object)

            Public Sub New(n As String, dt As Type, g As Func(Of Object, Object))
                Name = n
                DeclaredType = dt
                Getter = g
            End Sub
        End Class

        ''' <summary>
        ''' Builds an object schema from the public instance fields and properties of a type.
        ''' </summary>
        ''' <param name="t">The object type to inspect.</param>
        ''' <returns>A <see cref="DotNetSchema"/> whose kind is <c>Object</c>.</returns>
        ''' <remarks>
        ''' Member collection rules:
        '''   - public instance fields are included
        '''   - public instance properties with a getter are included
        '''   - static members are ignored
        '''   - indexed properties are ignored
        '''   - literal fields are ignored
        '''
        ''' Members are sorted by ordinal name so the generated schema key is stable.
        ''' Duplicate public member names are rejected.
        ''' </remarks>
        Private Shared Function BuildObjectSchema(t As Type) As DotNetSchema

            Dim flags As BindingFlags = BindingFlags.Instance Or BindingFlags.Public
            Dim tmp As New List(Of MemberMeta)()

            For Each finfo As FieldInfo In t.GetFields(flags)
                If finfo Is Nothing Then Continue For
                If finfo.IsStatic Then Continue For
                If finfo.IsLiteral Then Continue For
                tmp.Add(New MemberMeta(finfo.Name, finfo.FieldType, BuildGetter(finfo)))
            Next

            For Each pinfo As PropertyInfo In t.GetProperties(flags)
                If pinfo Is Nothing Then Continue For

                Dim getterMethod As MethodInfo = pinfo.GetGetMethod(nonPublic:=False)
                If getterMethod Is Nothing Then Continue For
                If getterMethod.IsStatic Then Continue For
                If pinfo.GetIndexParameters().Length > 0 Then Continue For

                tmp.Add(New MemberMeta(pinfo.Name, pinfo.PropertyType, BuildGetter(pinfo)))
            Next

            tmp.Sort(Function(a, b) StringComparer.Ordinal.Compare(a.Name, b.Name))

            For i As Integer = 1 To tmp.Count - 1
                If StringComparer.Ordinal.Equals(tmp(i - 1).Name, tmp(i).Name) Then
                    Throw New InvalidOperationException($"Invalid schema: duplicate public member '{tmp(i).Name}' in type {t.FullName}.")
                End If
            Next

            Dim metas(tmp.Count - 1) As DotNetFieldMeta

            For i As Integer = 0 To tmp.Count - 1

                Dim declaredType As Type = tmp(i).DeclaredType
                Dim typeCode As JsonFieldType = Cache.TypeToFieldType(declaredType)
                Dim baseType As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)

                If baseType = JsonFieldType.Unknown Then
                    Throw New NotSupportedException($"Unsupported type in schema: {declaredType.FullName}")
                End If

                Dim refType As Type = Nothing
                Dim refKey As String = ""

                If baseType = JsonFieldType.Object Then
                    refType = ResolveObjectSchemaType(declaredType)
                    refKey = GetRefSchemaKey(refType)
                End If

                metas(i) = New DotNetFieldMeta(
                    name:=tmp(i).Name,
                    typeCode:=typeCode,
                    refSchemaKey:=refKey,
                    getter:=tmp(i).Getter,
                    refDotNetType:=refType
                )
            Next

            Dim key As String = BuildObjectKey(metas)
            Return New DotNetSchema(t, key, metas)

        End Function

        ''' <summary>
        ''' Builds the canonical signature key for an object schema.
        ''' </summary>
        ''' <param name="metas">The ordered field metadata that defines the object shape.</param>
        ''' <returns>A stable string key used for schema identity and caching.</returns>
        ''' <remarks>
        ''' The generated key captures:
        '''   - member name
        '''   - member type code
        '''   - referenced schema key for object-like members
        '''
        ''' The <c>"obj-"</c> prefix guarantees the result is never empty and keeps
        ''' object keys in a separate namespace from map and array keys.
        ''' </remarks>
        Private Shared Function BuildObjectKey(metas As DotNetFieldMeta()) As String
            Dim sb As New StringBuilder(Math.Max(64, If(metas Is Nothing, 0, metas.Length) * 32))

            sb.Append("obj-")

            If metas IsNot Nothing Then
                For i As Integer = 0 To metas.Length - 1
                    sb.Append(metas(i).Name)
                    sb.Append(":"c)
                    sb.Append(CInt(metas(i).TypeCode))
                    sb.Append(":"c)
                    If metas(i).RefSchemaKey IsNot Nothing AndAlso metas(i).RefSchemaKey.Length <> 0 Then
                        sb.Append(metas(i).RefSchemaKey)
                    End If
                    sb.Append("-"c)
                Next
            End If

            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Resolves the schema key referenced by an object-like field or element type.
        ''' </summary>
        ''' <param name="refType">The referenced runtime type.</param>
        ''' <returns>The referenced schema key, or an empty string when no reference exists.</returns>
        ''' <remarks>
        ''' If the referenced type is currently being expanded on the active thread,
        ''' a cycle placeholder key is returned instead of recursing indefinitely.
        ''' </remarks>
        Private Shared Function GetRefSchemaKey(refType As Type) As String
            If refType Is Nothing Then Return ""

            If IsBuilding(refType) Then
                Return "@cycle"
            End If

            Return Cache.GetJsonSchemaKey(refType)
        End Function

        ''' <summary>
        ''' Normalizes a declared type into the concrete type whose schema should be referenced.
        ''' </summary>
        ''' <param name="declaredType">The declared member or element type.</param>
        ''' <returns>The normalized schema target type.</returns>
        ''' <remarks>
        ''' Normalization rules:
        '''   - unwrap array element types
        '''   - unwrap enumerable element types
        '''   - unwrap Nullable(Of T)
        ''' </remarks>
        Private Shared Function ResolveObjectSchemaType(declaredType As Type) As Type
            Dim t As Type = declaredType
            If t Is Nothing Then Return Nothing

            If t.IsArray Then
                t = t.GetElementType()
            Else
                Dim info = Cache.GetInfo(t)
                If info IsNot Nothing AndAlso info.EnumerableElement IsNot Nothing Then
                    t = info.EnumerableElement
                End If
            End If

            Dim u As Type = Nullable.GetUnderlyingType(t)
            If u IsNot Nothing Then t = u

            Return t
        End Function

        Private Shared Function ResolveMapValueRefType(dictType As Type) As Type
            If dictType Is Nothing Then Return Nothing

            Dim info = Cache.GetInfo(dictType)
            Dim valT As Type = If(info Is Nothing, Nothing, info.DictValue)

            If valT Is Nothing Then Return Nothing
            Return ResolveObjectSchemaType(valT)
        End Function

        Private Shared Function ResolveArrayElemDeclaredType(containerType As Type) As Type
            If containerType Is Nothing Then Return Nothing
            If containerType.IsArray Then Return containerType.GetElementType()

            Dim info = Cache.GetInfo(containerType)
            Return If(info Is Nothing, Nothing, info.EnumerableElement)
        End Function

        Private Shared Function ResolveArrayElemExpectedSchemaType(elemDeclared As Type) As Type
            If elemDeclared Is Nothing Then Return Nothing

            If elemDeclared.IsArray Then Return elemDeclared

            Dim ei = Cache.GetInfo(elemDeclared)
            If ei IsNot Nothing AndAlso
               ei.EnumerableElement IsNot Nothing AndAlso
               elemDeclared IsNot GetType(String) AndAlso
               elemDeclared IsNot GetType(Byte()) Then
                Return elemDeclared
            End If

            Return ResolveObjectSchemaType(elemDeclared)
        End Function

        ''' <summary>
        ''' Builds a boxed getter delegate for a field.
        ''' </summary>
        ''' <param name="f">The field metadata.</param>
        ''' <returns>A compiled delegate that reads the field value from an object instance.</returns>
        ''' <remarks>
        ''' The delegate signature is always <c>Func(Of Object, Object)</c> so it can be
        ''' stored uniformly inside schema metadata regardless of the actual member type.
        ''' </remarks>
        Private Shared Function BuildGetter(f As FieldInfo) As Func(Of Object, Object)
            If f Is Nothing Then Throw New ArgumentNullException(NameOf(f))
            If f.DeclaringType Is Nothing Then Throw New InvalidOperationException("FieldInfo has no DeclaringType.")
            Dim objParam As ParameterExpression = Expression.Parameter(GetType(Object), "obj")
            Dim typedObj As Expression = Expression.Convert(objParam, f.DeclaringType)
            Dim fieldExpr As Expression = Expression.Field(typedObj, f)
            Dim boxedExpr As Expression = Expression.Convert(fieldExpr, GetType(Object))
            Return Expression.Lambda(Of Func(Of Object, Object))(boxedExpr, objParam).Compile()
        End Function

        ''' <summary>
        ''' Builds a boxed getter delegate for a property.
        ''' </summary>
        ''' <param name="p">The property metadata.</param>
        ''' <returns>A compiled delegate that reads the property value from an object instance.</returns>
        ''' <remarks>
        ''' The delegate signature is always <c>Func(Of Object, Object)</c> so it can be
        ''' stored uniformly inside schema metadata regardless of the actual member type.
        ''' </remarks>
        Private Shared Function BuildGetter(p As PropertyInfo) As Func(Of Object, Object)
            If p Is Nothing Then Throw New ArgumentNullException(NameOf(p))
            If p.DeclaringType Is Nothing Then Throw New InvalidOperationException("PropertyInfo has no DeclaringType.")
            Dim objParam As ParameterExpression = Expression.Parameter(GetType(Object), "obj")
            Dim typedObj As Expression = Expression.Convert(objParam, p.DeclaringType)
            Dim propExpr As Expression = Expression.Property(typedObj, p)
            Dim boxedExpr As Expression = Expression.Convert(propExpr, GetType(Object))
            Return Expression.Lambda(Of Func(Of Object, Object))(boxedExpr, objParam).Compile()
        End Function

#Region "DotNetSchema -> JsonSchema"

        Public Shared Function DotNetSchemaToJsonSchema(
     dn As DotNet.DotNetSchema,
     Optional building As HashSet(Of String) = Nothing
 ) As JsonSchema

            If dn Is Nothing Then Throw New ArgumentNullException(NameOf(dn))
            If String.IsNullOrWhiteSpace(dn.JsonSchemaKey) Then
                Throw New Exception("DotNetSchema.JsonSchemaKey cannot be null/empty.")
            End If

            If building Is Nothing Then
                building = New HashSet(Of String)(StringComparer.Ordinal)
            End If

            Dim selfKey As String = dn.JsonSchemaKey

            If building.Contains(selfKey) Then
                Throw New Exception("DotNetSchemaToJsonSchema re-entered the same schema directly: " & selfKey)
            End If

            building.Add(selfKey)

            Try

                If dn.IsMap Then

                    Dim valueType As JsonFieldType = dn.MapValueTypeCode
                    Dim baseVal As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(valueType)
                    Dim dictValueSchemaKey As String = Nothing

                    If baseVal = JsonFieldType.Object Then

                        Dim refType As Type = ResolveMapValueRefType(dn.DotNetType)
                        If refType Is Nothing Then
                            Throw New Exception("Map Object/Object[] value without ref type: " & dn.DotNetType.FullName)
                        End If

                        Dim refDs As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(refType)
                        dictValueSchemaKey = refDs.JsonSchemaKey

                        If Not building.Contains(dictValueSchemaKey) Then
                            JSON.Cache.GetOrAdd(
                                refDs,
                                Function(x) DotNetSchemaToJsonSchema(x, building)
                            )
                        End If

                    End If

                    Return New JsonSchema(
                        selfKey,
                        JsonSchema.SchemaKind.Map,
                        valueType,
                        dictValueSchemaKey
                    )

                End If

                If dn.IsArray Then

                    Dim elemType As JsonFieldType = dn.ArrayValueTypeCode
                    Dim elemBase As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(elemType)
                    Dim arrayValueSchemaKey As String = Nothing

                    If elemBase = JsonFieldType.Object Then

                        Dim elemDeclared As Type = ResolveArrayElemDeclaredType(dn.DotNetType)
                        Dim refType As Type = ResolveArrayElemExpectedSchemaType(elemDeclared)

                        If refType Is Nothing Then
                            Throw New Exception("Array Object element without ref type: " & dn.DotNetType.FullName)
                        End If

                        Dim refDs As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(refType)
                        arrayValueSchemaKey = refDs.JsonSchemaKey

                        If Not building.Contains(arrayValueSchemaKey) Then
                            JSON.Cache.GetOrAdd(
                                refDs,
                                Function(x) DotNetSchemaToJsonSchema(x, building)
                            )
                        End If

                    End If

                    Return New JsonSchema(
                        selfKey,
                        JsonSchema.SchemaKind.Array,
                        elemType,
                        arrayValueSchemaKey
                    )

                End If

                Dim metas = dn.FieldsMeta
                Dim count As Integer = If(metas Is Nothing, 0, metas.Length)

                If count = 0 Then
                    Return New JsonSchema(selfKey, Array.Empty(Of JsonField)())
                End If

                Dim fields(count - 1) As JsonField

                For i As Integer = 0 To count - 1

                    Dim meta = metas(i)
                    If meta Is Nothing Then
                        Throw New Exception("DotNetSchema.FieldsMeta contains null entry: " & selfKey)
                    End If

                    Dim f As New JsonField With {
                        .Name = meta.Name,
                        .TypeCode = meta.TypeCode,
                        .RefSchemaKey = Nothing
                    }

                    If JsonField.FieldTypeIsObjectOrObjectArray(meta.TypeCode) Then

                        Dim refType As Type = meta.RefDotNetType
                        If refType Is Nothing Then
                            Throw New Exception("Object/Object[] field without RefDotNetType: " & meta.Name & " (schema=" & selfKey & ")")
                        End If

                        Dim refDs As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(refType)
                        f.RefSchemaKey = refDs.JsonSchemaKey

                        If Not building.Contains(f.RefSchemaKey) Then
                            JSON.Cache.GetOrAdd(
                                refDs,
                                Function(x) DotNetSchemaToJsonSchema(x, building)
                            )
                        End If

                    End If

                    fields(i) = f

                Next

                Return New JsonSchema(selfKey, fields)

            Finally
                building.Remove(selfKey)
            End Try

        End Function
#End Region

    End Class

End Namespace