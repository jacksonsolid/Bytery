Imports System.Collections.Concurrent
Imports System.Linq.Expressions
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Bytery.JSON

Namespace DotNet

    ''' <summary>
    ''' Central type-classification cache used by the reflection-based schema builder.
    ''' </summary>
    ''' <remarks>
    ''' This cache performs three related roles:
    '''
    ''' 1. Classify runtime types into protocol categories.
    ''' 2. Lazily memoize <see cref="DotNetSchema"/> instances for supported structured types.
    ''' 3. Lazily memoize canonical schema keys used to identify schemas globally.
    '''
    ''' The cache is process-wide and thread-safe. Each <see cref="Type"/> is analyzed at most once,
    ''' and the resulting metadata is reused by the encoder/session/schema pipeline.
    ''' </remarks>
    Friend NotInheritable Class Cache

        ''' <summary>
        ''' Static-only type.
        ''' </summary>
        Private Sub New()
        End Sub

        ''' <summary>
        ''' Cached classification and helper metadata for a .NET type.
        ''' </summary>
        ''' <remarks>
        ''' This structure mixes:
        '''   - cheap classification flags
        '''   - resolved dictionary/enumerable element information
        '''   - protocol-facing field type classification
        '''   - lazily populated schema/key references
        '''   - compiled accessors used for generic dictionary iteration
        '''
        ''' The same instance is reused for the lifetime of the process once inserted
        ''' into <see cref="_info"/>.
        ''' </remarks>
        Friend NotInheritable Class TypeInfo
            Public IsPrimitiveLike As Boolean
            Public IsDictionary As Boolean
            Public DictKey As Type
            Public DictValue As Type
            Public EnumerableElement As Type
            Public JsonFieldType As JsonFieldType

            Public DotNetSchema As DotNetSchema
            Public JsonSchemaKey As String

            Public DictCountGetter As Func(Of Object, Integer)
            Public DictEntryKeyGetter As Func(Of Object, Object)
            Public DictEntryValueGetter As Func(Of Object, Object)
        End Class

        ''' <summary>
        ''' Global cache indexed by runtime type.
        ''' </summary>
        Private Shared ReadOnly _info As New ConcurrentDictionary(Of Type, TypeInfo)()

        ''' <summary>
        ''' Gets cached classification metadata for a type, creating it on first use.
        ''' </summary>
        ''' <param name="t">The runtime type to classify.</param>
        ''' <returns>
        ''' The cached <see cref="TypeInfo"/> instance, or <c>Nothing</c> when <paramref name="t"/> is <c>Nothing</c>.
        ''' </returns>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Shared Function GetInfo(t As Type) As TypeInfo
            If t Is Nothing Then Return Nothing
            Return _info.GetOrAdd(t, AddressOf BuildInfo)
        End Function

        ''' <summary>
        ''' Gets the cached <see cref="DotNetSchema"/> for a type, creating it lazily when needed.
        ''' </summary>
        ''' <param name="t">The type whose schema should be returned.</param>
        ''' <returns>
        ''' The cached schema, or <c>Nothing</c> when <paramref name="t"/> is <c>Nothing</c>.
        ''' </returns>
        ''' <remarks>
        ''' The actual schema build is delegated to <see cref="Builder.BuildDotNetSchema(Type)"/>.
        ''' Publication is performed with <see cref="Interlocked.CompareExchange"/> so that concurrent
        ''' callers converge on a single stored instance.
        ''' </remarks>
        Public Shared Function GetDotNetSchema(t As Type) As DotNetSchema
            If t Is Nothing Then Return Nothing

            Dim info = GetInfo(t)
            Dim s = info.DotNetSchema
            If s IsNot Nothing Then Return s

            Dim built As DotNetSchema = Builder.BuildDotNetSchema(t)

            Dim prior = Interlocked.CompareExchange(info.DotNetSchema, built, Nothing)
            Return If(prior, built)
        End Function

        ''' <summary>
        ''' Gets the canonical JSON-schema key for a type, creating it lazily when needed.
        ''' </summary>
        ''' <param name="t">The type whose schema key should be returned.</param>
        ''' <returns>
        ''' The canonical schema key, or an empty string when the type is primitive-like or <c>Nothing</c>.
        ''' </returns>
        ''' <remarks>
        ''' Primitive-like types do not produce standalone object/map/array schemas in this layer,
        ''' so they intentionally return an empty key.
        ''' </remarks>
        Public Shared Function GetJsonSchemaKey(t As Type) As String
            If t Is Nothing Then Return ""

            Dim info = GetInfo(t)
            Dim k = info.JsonSchemaKey
            If k IsNot Nothing AndAlso k.Length <> 0 Then Return k

            If info.IsPrimitiveLike Then Return ""

            Dim s = GetDotNetSchema(t)
            Dim builtKey As String = s.JsonSchemaKey

            Dim prior = Interlocked.CompareExchange(info.JsonSchemaKey, builtKey, Nothing)
            Return If(prior, builtKey)
        End Function

        ''' <summary>
        ''' Builds the initial cached classification record for a runtime type.
        ''' </summary>
        ''' <param name="originalType">The original type requested by the caller.</param>
        ''' <returns>A fully populated <see cref="TypeInfo"/> record.</returns>
        ''' <remarks>
        ''' Classification flow:
        '''
        ''' 1. Unwrap nullable root types.
        ''' 2. Detect special scalar cases such as <c>Byte()</c> and enums.
        ''' 3. Detect dictionaries.
        ''' 4. Detect enumerable element types when the type is not a dictionary.
        ''' 5. Compute whether the root is primitive-like.
        ''' 6. Compute the protocol field type, with special care for array/enumerable roots.
        '''
        ''' Important detail:
        ''' for array/enumerable roots, the field type must be derived from the element type,
        ''' not from the container type itself. This is what lets, for example, <c>List(Of Integer)</c>
        ''' become <c>Integer Or ArrayFlag</c> instead of falling through as <c>Object</c>.
        ''' </remarks>
        Private Shared Function BuildInfo(originalType As Type) As TypeInfo
            Dim info As New TypeInfo()

            Dim t As Type = originalType
            Dim u As Type = Nullable.GetUnderlyingType(t)
            If u IsNot Nothing Then t = u

            If t Is GetType(Byte()) Then
                info.IsPrimitiveLike = True
                info.JsonFieldType = JSON.JsonFieldType.Bytes
                Return info
            End If

            If t.IsEnum Then
                info.IsPrimitiveLike = True
                info.JsonFieldType = JSON.JsonFieldType.Integer
                Return info
            End If

            info.IsDictionary = False
            info.DictKey = Nothing
            info.DictValue = Nothing

            ' Fast path for direct IDictionary(Of K,V) / Dictionary(Of K,V) shapes.
            If t.IsGenericType Then
                Dim gtd As Type = t.GetGenericTypeDefinition()

                If gtd = GetType(IDictionary(Of ,)) OrElse gtd = GetType(Dictionary(Of ,)) Then

                    Dim args = t.GetGenericArguments()
                    info.DictKey = args(0)
                    info.DictValue = args(1)
                    info.IsDictionary = True

                End If
            End If

            ' Fallback: discover generic dictionary support through implemented interfaces.
            If Not info.IsDictionary Then
                info.IsDictionary = ImplementsGenericDict(t, info.DictKey, info.DictValue)
            End If

            ' Last fallback: treat non-generic IDictionary implementations as dictionaries too.
            If Not info.IsDictionary Then
                info.IsDictionary = GetType(IDictionary).IsAssignableFrom(t)
            End If

            ' When K/V types are known, build compiled helpers for efficient generic dictionary access.
            If info.IsDictionary AndAlso info.DictKey IsNot Nothing AndAlso info.DictValue IsNot Nothing Then

                Dim kvpType As Type = GetType(KeyValuePair(Of ,)).MakeGenericType(info.DictKey, info.DictValue)

                Dim icoll As Type = GetType(ICollection(Of )).MakeGenericType(kvpType)
                Dim iroColl As Type = GetType(IReadOnlyCollection(Of )).MakeGenericType(kvpType)

                Dim countItf As Type =
                    If(icoll.IsAssignableFrom(t), icoll,
                       If(iroColl.IsAssignableFrom(t), iroColl, Nothing))

                If countItf IsNot Nothing Then
                    info.DictCountGetter = BuildCountGetter(countItf)
                    info.DictEntryKeyGetter = BuildKvpGetter(kvpType, "Key")
                    info.DictEntryValueGetter = BuildKvpGetter(kvpType, "Value")
                End If

            End If

            ' Only non-dictionary, non-string, non-byte[] containers participate in enumerable element discovery.
            If t IsNot GetType(String) AndAlso t IsNot GetType(Byte()) AndAlso Not info.IsDictionary Then
                info.EnumerableElement = TryGetEnumerableElement(t)
            End If

            info.IsPrimitiveLike = (Type.GetTypeCode(t) <> TypeCode.Object)

            ' Array/enumerable roots are classified by element type.
            Dim isArray As Boolean = originalType.IsArray OrElse (info.EnumerableElement IsNot Nothing)

            Dim elem As Type = Nothing
            If originalType.IsArray Then
                elem = originalType.GetElementType()
            ElseIf info.EnumerableElement IsNot Nothing Then
                elem = info.EnumerableElement
            Else
                elem = t
            End If

            Dim eu As Type = Nullable.GetUnderlyingType(elem)
            If eu IsNot Nothing Then elem = eu

            Dim baseFt As JSON.JsonFieldType

            If elem Is GetType(Byte()) Then
                baseFt = JSON.JsonFieldType.Bytes

            ElseIf elem.IsEnum Then
                baseFt = JSON.JsonFieldType.Integer

            Else
                Select Case Type.GetTypeCode(elem)
                    Case TypeCode.Boolean : baseFt = JSON.JsonFieldType.Boolean
                    Case TypeCode.Single : baseFt = JSON.JsonFieldType.Float4Bytes
                    Case TypeCode.Double, TypeCode.Decimal : baseFt = JSON.JsonFieldType.Float8Bytes
                    Case TypeCode.DateTime : baseFt = JSON.JsonFieldType.Date
                    Case TypeCode.String : baseFt = JSON.JsonFieldType.String

                    Case TypeCode.Byte, TypeCode.SByte, TypeCode.Int16, TypeCode.UInt16,
                 TypeCode.Int32, TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64
                        baseFt = JSON.JsonFieldType.Integer

                    Case TypeCode.Object
                        baseFt = JSON.JsonFieldType.Object

                    Case Else
                        baseFt = JSON.JsonFieldType.Unknown
                End Select
            End If

            info.JsonFieldType = baseFt
            If isArray AndAlso baseFt <> JSON.JsonFieldType.Unknown Then
                info.JsonFieldType = baseFt Or JSON.JsonFieldType.ArrayFlag
            End If

            Return info
        End Function

        ''' <summary>
        ''' Builds a boxed count getter for a generic dictionary collection interface.
        ''' </summary>
        ''' <param name="dictInterface">An interface such as ICollection(Of KeyValuePair(Of K,V)).</param>
        ''' <returns>A compiled delegate that returns the collection count from a boxed instance.</returns>
        Private Shared Function BuildCountGetter(dictInterface As Type) As Func(Of Object, Integer)
            Dim o = Expression.Parameter(GetType(Object), "o")
            Dim castItf = Expression.Convert(o, dictInterface)
            Dim countProp = Expression.Property(castItf, "Count")
            Return Expression.Lambda(Of Func(Of Object, Integer))(countProp, o).Compile()
        End Function

        ''' <summary>
        ''' Builds a boxed getter for a property of a boxed <c>KeyValuePair(Of K,V)</c>.
        ''' </summary>
        ''' <param name="kvpType">The closed generic KeyValuePair type.</param>
        ''' <param name="propName">The property name to read, typically <c>"Key"</c> or <c>"Value"</c>.</param>
        ''' <returns>A compiled delegate that reads the requested property from a boxed key/value pair.</returns>
        Private Shared Function BuildKvpGetter(kvpType As Type, propName As String) As Func(Of Object, Object)
            Dim kvpObj = Expression.Parameter(GetType(Object), "kvpObj")
            Dim castKvp = Expression.Convert(kvpObj, kvpType)
            Dim prop = Expression.Property(castKvp, propName)
            Dim box = Expression.Convert(prop, GetType(Object))
            Return Expression.Lambda(Of Func(Of Object, Object))(box, kvpObj).Compile()
        End Function

        ''' <summary>
        ''' Returns whether a type implements a supported generic dictionary interface.
        ''' </summary>
        ''' <param name="t">The type to inspect.</param>
        ''' <param name="k">Receives the discovered key type when found.</param>
        ''' <param name="v">Receives the discovered value type when found.</param>
        ''' <returns><c>True</c> when a supported generic dictionary interface is found; otherwise <c>False</c>.</returns>
        Private Shared Function ImplementsGenericDict(t As Type, ByRef k As Type, ByRef v As Type) As Boolean
            For Each itf In t.GetInterfaces()
                If itf.IsGenericType Then
                    Dim gtd = itf.GetGenericTypeDefinition()
                    If gtd Is GetType(IDictionary(Of ,)) OrElse gtd Is GetType(IReadOnlyDictionary(Of ,)) Then
                        Dim args = itf.GetGenericArguments()
                        k = args(0) : v = args(1)
                        Return True
                    End If
                End If
            Next
            Return False
        End Function

        ''' <summary>
        ''' Attempts to extract the element type from an array or IEnumerable(Of T) shape.
        ''' </summary>
        ''' <param name="t">The type to inspect.</param>
        ''' <returns>The discovered element type, or <c>Nothing</c> when the type is not a supported enumerable shape.</returns>
        Private Shared Function TryGetEnumerableElement(t As Type) As Type
            If t.IsArray Then Return t.GetElementType()

            If t.IsGenericType AndAlso t.GetGenericTypeDefinition() Is GetType(IEnumerable(Of )) Then
                Return t.GetGenericArguments()(0)
            End If

            For Each itf In t.GetInterfaces()
                If itf.IsGenericType AndAlso itf.GetGenericTypeDefinition() Is GetType(IEnumerable(Of )) Then
                    Return itf.GetGenericArguments()(0)
                End If
            Next

            Return Nothing
        End Function

        ''' <summary>
        ''' Returns whether the specified type is primitive-like for schema purposes.
        ''' </summary>
        ''' <param name="t">The type to inspect.</param>
        ''' <returns><c>True</c> when the type is treated as a scalar/blob value instead of a structured schema.</returns>
        Public Shared Function TypeIsPrimitiveLikeForSchema(t As Type) As Boolean
            Dim i = GetInfo(t)
            Return i IsNot Nothing AndAlso i.IsPrimitiveLike
        End Function

        ''' <summary>
        ''' Returns whether the specified type is classified as a dictionary.
        ''' </summary>
        ''' <param name="t">The type to inspect.</param>
        ''' <returns><c>True</c> when the type is treated as a map by the schema builder.</returns>
        Public Shared Function TypeIsDictionary(t As Type) As Boolean
            Dim i = GetInfo(t)
            Return i IsNot Nothing AndAlso i.IsDictionary
        End Function

        ''' <summary>
        ''' Returns the protocol field type classification for a runtime type.
        ''' </summary>
        ''' <param name="t">The type to classify.</param>
        ''' <returns>
        ''' The resolved <see cref="JsonFieldType"/>, or <see cref="JsonFieldType.Unknown"/> when <paramref name="t"/> is <c>Nothing</c>.
        ''' </returns>
        Public Shared Function TypeToFieldType(t As Type) As JsonFieldType
            Dim i = GetInfo(t)
            If i Is Nothing Then Return JsonFieldType.Unknown
            Return i.JsonFieldType
        End Function

    End Class

End Namespace