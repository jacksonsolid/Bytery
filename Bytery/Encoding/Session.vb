Imports System.IO
Imports System.Runtime.CompilerServices
Imports Bytery.JSON

Namespace Encoding

    ''' <summary>
    ''' Per-encode session state.
    ''' 
    ''' Responsibilities:
    ''' - Build and assign session-local schema pointers.
    ''' - Build and assign session-local string pointers.
    ''' - Build and assign session-local date pointers.
    ''' - Track optional FILES zone entries.
    ''' - Expose current zone presence through ZMSK.
    ''' - Serialize the header, files zone, cache tables, schema table, and data section.
    ''' - Detect cyclic references while traversing object graphs.
    ''' 
    ''' Important:
    ''' Global caches live outside this class. This type manages only the
    ''' temporary state required for a single encoding session.
    ''' </summary>
    Friend NotInheritable Class Session
        Implements ISession

#Region "Session Schema Table (per-session pointers)"

        ''' <summary>
        ''' Ordered list of schemas emitted in this session.
        ''' The position in this list is the schema index used by session pointers.
        ''' </summary>
        Private ReadOnly _schemaList As New List(Of SessionSchema)()

        ''' <summary>
        ''' Fast lookup from global JsonSchema key to the session-local schema wrapper.
        ''' </summary>
        Private ReadOnly _schemaByKey As New Dictionary(Of String, SessionSchema)(StringComparer.Ordinal)
        Private ReadOnly _dotNetByJsonKey As New Dictionary(Of String, DotNet.DotNetSchema)(StringComparer.Ordinal)

        ''' <summary>
        ''' Tracks which session schemas have already had all of their internal
        ''' references resolved for this session.
        ''' </summary>
        Private ReadOnly _initialized As New HashSet(Of String)(StringComparer.Ordinal)
        Private ReadOnly _initializing As New HashSet(Of String)(StringComparer.Ordinal)

        Private ReadOnly _expandingDependencies As New HashSet(Of String)(StringComparer.Ordinal)
        Private ReadOnly _expandedDependencies As New HashSet(Of String)(StringComparer.Ordinal)


        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function EnsureSchema(ds As DotNet.DotNetSchema) As SessionSchema
            If ds Is Nothing Then Throw New ArgumentNullException(NameOf(ds))
            If String.IsNullOrEmpty(ds.JsonSchemaKey) Then Throw New Exception("DotNetSchema.JsonSchemaKey cannot be null/empty.")

            Dim js As JsonSchema = JSON.Cache.GetOrAdd(ds, AddressOf BuildJsonSchemaFromDotNet)

            If Not _dotNetByJsonKey.ContainsKey(js.Key) Then
                _dotNetByJsonKey.Add(js.Key, ds)
            End If

            Dim ss As SessionSchema = EnsureSchema(js)

            ExpandDotNetDependencies(ds)
            InitializeSessionSchema(ss)

            Return ss
        End Function

        ''' <summary>
        ''' Resolves a runtime .NET type into a session-local schema.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function EnsureSchemaByType(t As Type) As SessionSchema
            If t Is Nothing Then Throw New ArgumentNullException(NameOf(t))
            Dim ds As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(t)
            Return EnsureSchema(ds)
        End Function

        ''' <summary>
        ''' Ensures that a session wrapper exists for the given global JsonSchema.
        ''' 
        ''' Session pointers are assigned here:
        ''' - first schema added gets index 0
        ''' - second schema added gets index 1
        ''' - etc.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function EnsureSchema(js As JsonSchema) As SessionSchema
            If js Is Nothing Then Throw New ArgumentNullException(NameOf(js))
            If String.IsNullOrEmpty(js.Key) Then Throw New Exception("JsonSchema.Key cannot be null/empty.")

            Dim existing As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(js.Key, existing) Then Return existing

            Dim idx As Integer = _schemaList.Count
            Dim ptr = Codec.SchemaPointer(idx)

            Dim ss As New SessionSchema(js) With {
                .Index = idx,
                .Pointer = ptr
            }

            _schemaList.Add(ss)
            _schemaByKey.Add(js.Key, ss)

            Return ss
        End Function

        ''' <summary>
        ''' Ensures that a session schema exists for a known global JsonSchema key.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function EnsureSchemaByKey(key As String) As SessionSchema
            If String.IsNullOrEmpty(key) Then Throw New ArgumentNullException(NameOf(key))

            Dim ss As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(key, ss) Then Return ss

            Dim js As JsonSchema = Nothing
            If Not JSON.Cache.TryGet(key, js) Then
                Throw New Exception("Missing JsonSchema in global JSON.Cache for key: " & key)
            End If

            ss = EnsureSchema(js)
            InitializeSessionSchema(ss)
            Return ss
        End Function

        ''' <summary>
        ''' Converts a DotNet schema into the corresponding global JsonSchema.
        ''' </summary>
        Private Shared Function BuildJsonSchemaFromDotNet(dn As DotNet.DotNetSchema) As JsonSchema
            Return DotNet.Builder.DotNetSchemaToJsonSchema(dn)
        End Function

        ''' <summary>
        ''' Ensures that every object-valued dependency referenced by the provided
        ''' DotNet schema has already been materialized in the session.
        ''' 
        ''' This step is required because session pointers can only be resolved after
        ''' the referenced schemas exist in the session schema table.
        ''' </summary>
        Private Sub ExpandDotNetDependencies(ds As DotNet.DotNetSchema)

            If ds Is Nothing Then Return
            If String.IsNullOrEmpty(ds.JsonSchemaKey) Then
                Throw New Exception("DotNetSchema.JsonSchemaKey cannot be null/empty.")
            End If

            Dim key As String = ds.JsonSchemaKey

            ' Já foi expandido completamente nesta sessão.
            If _expandedDependencies.Contains(key) Then
                Return
            End If

            ' Já está em expansão no stack atual.
            ' Isso resolve autorreferência e referências mútuas.
            If _expandingDependencies.Contains(key) Then
                Return
            End If

            _expandingDependencies.Add(key)

            Try

                If ds.IsMap Then
                    Dim baseVal As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(ds.MapValueTypeCode)

                    If baseVal = JsonFieldType.Object Then
                        Dim refType As Type = ResolveMapValueRefType(ds.DotNetType)
                        If refType IsNot Nothing Then
                            EnsureSchemaByType(refType)
                        End If
                    End If

                    _expandedDependencies.Add(key)
                    Return
                End If

                If ds.IsArray Then
                    Dim elemBase As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(ds.ArrayValueTypeCode)

                    If elemBase = JsonFieldType.Object Then
                        Dim elemDeclared As Type = ResolveArrayElemDeclaredType(ds.DotNetType)
                        Dim refType As Type = ResolveArrayElemExpectedSchemaType(elemDeclared)

                        If refType IsNot Nothing Then
                            EnsureSchemaByType(refType)
                        End If
                    End If

                    _expandedDependencies.Add(key)
                    Return
                End If

                Dim metas = ds.FieldsMeta
                If metas Is Nothing OrElse metas.Length = 0 Then
                    _expandedDependencies.Add(key)
                    Return
                End If

                For i As Integer = 0 To metas.Length - 1
                    Dim tc As JsonFieldType = metas(i).TypeCode
                    Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(tc)

                    If baseTc = JsonFieldType.Object Then
                        Dim refType As Type = metas(i).RefDotNetType
                        If refType IsNot Nothing Then
                            EnsureSchemaByType(refType)
                        End If
                    End If
                Next

                _expandedDependencies.Add(key)

            Finally
                _expandingDependencies.Remove(key)
            End Try

        End Sub

        Private Sub InitializeSessionSchema(ss As SessionSchema)
            If ss Is Nothing OrElse ss.JS Is Nothing Then Return

            Dim key As String = ss.JS.Key

            If _initialized.Contains(key) Then Return
            If _initializing.Contains(key) Then Return

            Dim ds As DotNet.DotNetSchema = Nothing
            If Not _dotNetByJsonKey.TryGetValue(key, ds) Then
                Throw New Exception("Missing DotNet schema mapping for JsonSchema key: " & key)
            End If

            _initializing.Add(key)

            Try

                Select Case ss.JS.Kind

                    Case JsonSchema.SchemaKind.Map

                        Dim vt As JsonFieldType = ds.MapValueTypeCode
                        Dim baseVal As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(vt)

                        If baseVal < JsonFieldType.Integer OrElse baseVal > JsonFieldType.Object Then
                            Throw New Exception("Invalid map DictValueType base for schema: " & ss.JS.Key & " => " & vt.ToString())
                        End If

                        If baseVal = JsonFieldType.Object Then
                            Dim refType As Type = ResolveMapValueRefType(ds.DotNetType)
                            If refType Is Nothing Then
                                Throw New Exception("Map value expectedRefType is missing for schema: " & ss.JS.Key)
                            End If

                            Dim refSs As SessionSchema = EnsureSchemaByType(refType)
                            ss.DictValueSchemaPtr = refSs.Pointer
                        End If

                        ss.Fields = Array.Empty(Of SessionField)()
                        _initialized.Add(key)
                        Return

                    Case JsonSchema.SchemaKind.Array

                        Dim et As JsonFieldType = ds.ArrayValueTypeCode

                        If JsonField.FieldTypeIsArray(et) Then
                            Throw New Exception("Array schema ArrayValueType cannot have ArrayFlag: " & et.ToString())
                        End If

                        Dim baseElem As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(et)

                        If baseElem = JsonFieldType.Object Then
                            Dim elemDeclared As Type = ResolveArrayElemDeclaredType(ds.DotNetType)
                            Dim refType As Type = ResolveArrayElemExpectedSchemaType(elemDeclared)

                            If refType Is Nothing Then
                                Throw New Exception("Array element expectedRefType is missing for schema: " & ss.JS.Key)
                            End If

                            Dim refSs As SessionSchema = EnsureSchemaByType(refType)
                            ss.ArrayValueSchemaPtr = refSs.Pointer
                        End If

                        ss.Fields = Array.Empty(Of SessionField)()
                        _initialized.Add(key)
                        Return

                    Case Else

                        Dim jf As JsonField() = ss.JS.Fields
                        Dim metas = ds.FieldsMeta

                        Dim count As Integer = If(jf Is Nothing, 0, jf.Length)

                        If count = 0 Then
                            ss.Fields = Array.Empty(Of SessionField)()
                            _initialized.Add(key)
                            Return
                        End If

                        If metas Is Nothing OrElse metas.Length <> count Then
                            Throw New Exception("DotNet fields metadata mismatch for schema: " & ss.JS.Key)
                        End If

                        Dim out(count - 1) As SessionField

                        For i As Integer = 0 To count - 1
                            Dim f As JsonField = jf(i)
                            If f Is Nothing Then Throw New Exception("JsonSchema.Fields contains a null entry: " & ss.JS.Key)

                            Dim sf As New SessionField(f)

                            If JsonField.FieldTypeIsObjectOrObjectArray(f.TypeCode) Then
                                Dim refType As Type = metas(i).RefDotNetType

                                If refType Is Nothing Then
                                    Throw New Exception("Object field without RefDotNetType: " & f.Name & " (schema=" & ss.JS.Key & ")")
                                End If

                                Dim refSs As SessionSchema = EnsureSchemaByType(refType)
                                sf.RefSchemaPtr = refSs.Pointer
                            End If

                            out(i) = sf
                        Next

                        ss.Fields = out
                        _initialized.Add(key)
                        Return

                End Select

            Finally
                _initializing.Remove(key)
            End Try
        End Sub

        ''' <summary>
        ''' Resolves the map value reference type from a dictionary container type.
        ''' This is used only when the map value base type is Object.
        ''' </summary>
        Private Shared Function ResolveMapValueRefType(dictType As Type) As Type
            If dictType Is Nothing Then Return Nothing
            Dim info = DotNet.Cache.GetInfo(dictType)
            Dim valT As Type = If(info Is Nothing, Nothing, info.DictValue)
            If valT Is Nothing Then Return Nothing
            Return ResolveObjectSchemaType(valT)
        End Function

        ''' <summary>
        ''' Normalizes a declared object reference type into the concrete type that
        ''' should own the referenced schema.
        ''' 
        ''' Rules:
        ''' - unwrap arrays to their element type
        ''' - unwrap enumerable containers to their element type
        ''' - unwrap Nullable(Of T)
        ''' </summary>
        Private Shared Function ResolveObjectSchemaType(declaredType As Type) As Type
            Dim t As Type = declaredType
            If t Is Nothing Then Return Nothing

            If t.IsArray Then
                t = t.GetElementType()
            Else
                Dim info = DotNet.Cache.GetInfo(t)
                If info IsNot Nothing AndAlso info.EnumerableElement IsNot Nothing Then
                    t = info.EnumerableElement
                End If
            End If

            Dim u As Type = Nullable.GetUnderlyingType(t)
            If u IsNot Nothing Then t = u
            Return t
        End Function

#End Region

#Region "Cyclic Reference Protection"

        ''' <summary>
        ''' Reference-equality comparer used for the active recursion stack.
        ''' 
        ''' This ensures that cycle detection is based on object identity, not
        ''' value equality or custom Equals implementations.
        ''' </summary>
        Private NotInheritable Class RefEqComparer
            Implements IEqualityComparer(Of Object)

            Public Shared ReadOnly Instance As New RefEqComparer()

            Public Overloads Function Equals(x As Object, y As Object) As Boolean _
                Implements IEqualityComparer(Of Object).Equals
                Return Object.ReferenceEquals(x, y)
            End Function

            Public Overloads Function GetHashCode(obj As Object) As Integer _
                Implements IEqualityComparer(Of Object).GetHashCode
                If obj Is Nothing Then Return 0
                Return RuntimeHelpers.GetHashCode(obj)
            End Function
        End Class

        ''' <summary>
        ''' Tracks the objects currently being traversed in the active encode stack.
        ''' </summary>
        Private ReadOnly _activeRefs As New HashSet(Of Object)(RefEqComparer.Instance)

        ''' <summary>
        ''' Returns <see langword="True"/> only for reference types that need cycle tracking.
        ''' 
        ''' Types excluded from cycle tracking:
        ''' - Nothing
        ''' - value types
        ''' - String
        ''' - Byte()
        ''' </summary>
        Private Shared Function NeedsRefTrack(value As Object) As Boolean
            If value Is Nothing Then Return False
            Dim t = value.GetType()
            If t.IsValueType Then Return False
            If t Is GetType(String) Then Return False
            If t Is GetType(Byte()) Then Return False
            Return True
        End Function

        ''' <summary>
        ''' Pushes an object into the active reference stack and throws if the same
        ''' object is encountered again before leaving the current traversal path.
        ''' </summary>
        Private Sub EnterRef(value As Object)
            If Not NeedsRefTrack(value) Then Return
            If _activeRefs.Contains(value) Then
                Throw New InvalidOperationException($"Referência cíclica detectada: {value.GetType().FullName}")
            End If
            _activeRefs.Add(value)
        End Sub

        ''' <summary>
        ''' Removes an object from the active reference stack.
        ''' </summary>
        Private Sub ExitRef(value As Object)
            If Not NeedsRefTrack(value) Then Return
            _activeRefs.Remove(value)
        End Sub

#End Region

#Region "Container Zones (ZMSK presence byte)"

        ''' <summary>
        ''' Returns whether this session currently contains a STRING TABLE zone.
        ''' </summary>
        Friend ReadOnly Property HasStringTable As Boolean
            Get
                Return _cacheStrings.Count <> 0
            End Get
        End Property

        ''' <summary>
        ''' Returns whether this session currently contains a DATE TABLE zone.
        ''' </summary>
        Friend ReadOnly Property HasDateTable As Boolean
            Get
                Return _cacheDates.Count <> 0
            End Get
        End Property

        ''' <summary>
        ''' Returns whether this session currently contains a SCHEMA TABLE zone.
        ''' </summary>
        Friend ReadOnly Property HasSchemaTable As Boolean
            Get
                Return _schemaList.Count <> 0
            End Get
        End Property

        ''' <summary>
        ''' Builds the current first-byte ZMSK for this session.
        ''' 
        ''' Canonical zone order:
        ''' [header][files][string table][date table][schema table][data]
        ''' 
        ''' Notes:
        ''' - v1 uses only the first mask byte.
        ''' - ZMSK_HAS_NEXT is intentionally not set here.
        ''' - Presence only: zone order is still defined by the container format.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function GetZoneMask(Optional includeData As Boolean = True) As Byte

            Dim mask As Byte = 0

            If HasHeader Then mask = CByte(mask Or ZMSK_HEADERS)
            If HasFiles Then mask = CByte(mask Or ZMSK_FILES)
            If HasStringTable Then mask = CByte(mask Or ZMSK_STRING_TABLE)
            If HasDateTable Then mask = CByte(mask Or ZMSK_DATE_TABLE)
            If HasSchemaTable Then mask = CByte(mask Or ZMSK_SCHEMA_TABLE)
            If includeData Then mask = CByte(mask Or ZMSK_DATA)

            Return mask

        End Function

        ''' <summary>
        ''' Writes the current first-byte ZMSK for this session.
        ''' 
        ''' Layout:
        ''' [1 byte zmsk]
        ''' 
        ''' Canonical zone order:
        ''' [header][files][string table][date table][schema table][data]
        ''' 
        ''' Notes:
        ''' - v1 does not emit continuation bytes.
        ''' - This method writes only the mask byte; zone bodies are written
        '''   separately by the caller in canonical protocol order.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Sub WriteZoneMask(ms As Stream, Optional includeData As Boolean = True)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            ms.WriteByte(GetZoneMask(includeData))

        End Sub

#End Region

#Region "Header Cache (file header pairs)"

        ''' <summary>
        ''' Indicates whether the file should contain a header zone.
        ''' 
        ''' Notes:
        ''' - False => the HEADER zone is omitted entirely.
        ''' - True with zero entries => an empty HEADER zone is emitted.
        ''' </summary>
        Private _hasHeader As Boolean = False

        ''' <summary>
        ''' Ordered header entries.
        ''' </summary>
        Private ReadOnly _headerList As New List(Of HeaderEntry)()

        ''' <summary>
        ''' Maps header key to its position in <see cref="_headerList"/> so updates
        ''' can replace the existing entry without changing insertion order.
        ''' </summary>
        Private ReadOnly _headerIndexByKey As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

        ''' <summary>
        ''' Forces header emission even if the header contains zero pairs.
        ''' </summary>
        Public Sub EnableHeader()
            _hasHeader = True
        End Sub

        ''' <summary>
        ''' Returns whether this session will emit a header section.
        ''' </summary>
        Friend ReadOnly Property HasHeader As Boolean
            Get
                Return _hasHeader
            End Get
        End Property

        ''' <summary>
        ''' Adds or replaces a header entry using an explicit type code.
        ''' 
        ''' This overload is the one to use when the value may be <see langword="Nothing"/>.
        ''' Header supports only primitive wire types and primitive arrays.
        ''' Object values are not allowed.
        ''' </summary>
        Public Sub AddHeader(key As String, typeCode As JsonFieldType, value As Object)

            If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentNullException(NameOf(key))

            Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)
            Dim isArr As Boolean = JsonField.FieldTypeIsArray(typeCode)

            If baseTc < JsonFieldType.Integer OrElse baseTc > JsonFieldType.Bytes Then
                Throw New NotSupportedException("Header typeCode not supported: " & typeCode.ToString())
            End If

            ' Minimal runtime validation for common ambiguous cases.
            If Not isArr Then
                Select Case baseTc
                    Case JsonFieldType.Bytes
                        If value IsNot Nothing AndAlso Not TypeOf value Is Byte() Then
                            Throw New Exception("Header Bytes value must be Byte() or Nothing.")
                        End If
                End Select
            End If

            _hasHeader = True

            Dim e As New HeaderEntry(key, typeCode, value)

            Dim idx As Integer
            If _headerIndexByKey.TryGetValue(key, idx) Then
                _headerList(idx) = e
            Else
                idx = _headerList.Count
                _headerList.Add(e)
                _headerIndexByKey.Add(key, idx)
            End If

        End Sub

        ''' <summary>
        ''' Adds or replaces a header entry by inferring the wire type from the
        ''' runtime type of the provided value.
        ''' 
        ''' This overload does not allow <see langword="Nothing"/>, because the type
        ''' would be ambiguous.
        ''' </summary>
        Public Sub AddHeader(key As String, value As Object)

            If value Is Nothing Then
                Throw New Exception("Header value is Nothing; use AddHeader(key, typeCode, Nothing) to disambiguate.")
            End If

            Dim tc As JsonFieldType = DotNet.Cache.TypeToFieldType(value.GetType())
            Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(tc)

            If baseTc < JsonFieldType.Integer OrElse baseTc > JsonFieldType.Bytes Then
                Throw New NotSupportedException("Header inferred type not supported: " & tc.ToString() &
                                                " (runtime=" & value.GetType().FullName & ")")
            End If

            AddHeader(key, tc, value)

        End Sub

        ''' <summary>
        ''' Writes the header zone body.
        ''' 
        ''' Wire layout:
        ''' [LUINT headerByteLength][LUINT pairCount][header entries...]
        ''' 
        ''' Each header entry is encoded as:
        ''' [LSTR literal key][1-byte typeCode][payload]
        ''' 
        ''' Notes:
        ''' - Header absence is no longer encoded inside this zone.
        ''' - When no header is present, this method writes nothing.
        ''' - Zone presence is declared by ZMSK at the container level.
        ''' - headerByteLength covers the complete serialized header payload
        '''   that follows it, including the LUINT pairCount and all entries.
        ''' </summary>
        Public Sub WriteHeader(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If Not _hasHeader Then Return

            Dim count As Integer = _headerList.Count

            Using payload As New MemoryStream()

                ' Write pairCount first so headerByteLength includes its serialized LUINT size.
                Writer.WriteLUINT(CLng(count), payload)

                For i As Integer = 0 To count - 1
                    Dim e = _headerList(i)

                    WriteHeaderLstrLiteral(payload, e.Key)
                    payload.WriteByte(CByte(e.TypeCode))
                    WriteHeaderValue(payload, e.TypeCode, e.Value)
                Next

                Writer.WriteLUINT(CLng(payload.Length), ms)

                payload.Position = 0
                payload.CopyTo(ms)

            End Using

        End Sub

        ''' <summary>
        ''' Writes a header string in literal LSTR form.
        ''' Header strings never use session pointers.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Sub WriteHeaderLstrLiteral(ms As Stream, s As String)
            Dim p As PTR = Codec.LSTRFromString(s)
            ms.Write(p.len)
            ms.Write(p.data)
        End Sub

        ''' <summary>
        ''' Dispatches header value writing to either the scalar or array path,
        ''' based on the ArrayFlag bit.
        ''' </summary>
        Private Shared Sub WriteHeaderValue(ms As Stream, typeCode As JsonFieldType, value As Object)

            Dim isArr As Boolean = JsonField.FieldTypeIsArray(typeCode)
            Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)

            If isArr Then
                WriteHeaderArray(ms, baseTc, value)
            Else
                WriteHeaderScalar(ms, baseTc, value)
            End If

        End Sub

        ''' <summary>
        ''' Writes a scalar header value using header-compatible codecs.
        ''' </summary>
        Private Shared Sub WriteHeaderScalar(ms As Stream, baseType As JsonFieldType, value As Object)

            Select Case baseType

                Case JsonFieldType.Integer
                    Writer.WriteLINT(value, ms)

                Case JsonFieldType.Float4Bytes
                    Writer.WriteSingle(value, ms)

                Case JsonFieldType.Float8Bytes
                    Writer.WriteDouble(value, ms)

                Case JsonFieldType.Boolean
                    Writer.WriteBool(value, ms)

                Case JsonFieldType.Date
                    Writer.WriteDate(value, ms)

                Case JsonFieldType.String
                    WriteHeaderLstrLiteral(ms, If(value Is Nothing, Nothing, CStr(value)))

                Case JsonFieldType.Bytes
                    If value Is Nothing Then
                        ms.WriteByte(LUINT_NULL)
                    Else
                        Dim b As Byte() = DirectCast(value, Byte())
                        Dim p As PTR = Codec.FromBytes(b)
                        ms.Write(p.len)
                        ms.Write(p.data)
                    End If

                Case Else
                    Throw New NotSupportedException("Header scalar base type not supported: " & baseType.ToString())

            End Select

        End Sub

        ''' <summary>
        ''' Writes a header array value.
        ''' 
        ''' Null array:
        ''' - encoded as LUINT_NULL
        ''' 
        ''' Non-null array:
        ''' - encoded as [LUINT count][elements...]
        ''' </summary>
        Private Shared Sub WriteHeaderArray(ms As Stream, baseType As JsonFieldType, value As Object)

            If value Is Nothing Then
                ms.WriteByte(LUINT_NULL)
                Return
            End If

            Dim arr As Array = TryCast(value, Array)
            If arr Is Nothing Then
                Dim en As IEnumerable = TryCast(value, IEnumerable)
                If en Is Nothing Then Throw New NotSupportedException("Header ArrayFlag set but value is not Array/IEnumerable.")
                Dim tmp As New List(Of Object)()
                For Each it As Object In en
                    tmp.Add(it)
                Next
                Writer.WriteLUINT(CLng(tmp.Count), ms)
                For i As Integer = 0 To tmp.Count - 1
                    WriteHeaderArrayElem(ms, baseType, tmp(i))
                Next
                Return
            End If

            Writer.WriteLUINT(CLng(arr.Length), ms)
            For i As Integer = 0 To arr.Length - 1
                WriteHeaderArrayElem(ms, baseType, arr.GetValue(i))
            Next

        End Sub

        ''' <summary>
        ''' Writes a single header array element.
        ''' 
        ''' Strings and Byte() values need dedicated handling because their payload
        ''' layout is variable-width.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Sub WriteHeaderArrayElem(ms As Stream, baseType As JsonFieldType, it As Object)

            Select Case baseType
                Case JsonFieldType.String
                    WriteHeaderLstrLiteral(ms, If(it Is Nothing, Nothing, CStr(it)))

                Case JsonFieldType.Bytes
                    If it Is Nothing Then
                        ms.WriteByte(LUINT_NULL)
                    Else
                        If Not TypeOf it Is Byte() Then
                            Throw New Exception("Header Bytes[] element must be Byte() or Nothing.")
                        End If
                        Dim p As PTR = Codec.FromBytes(DirectCast(it, Byte()))
                        ms.Write(p.len)
                        ms.Write(p.data)
                    End If

                Case Else
                    WriteHeaderScalar(ms, baseType, it)
            End Select

        End Sub

#End Region

#Region "Date Cache (per-session date-table) - add on 2nd occurrence"

        ''' <summary>
        ''' Ordered date-table rows for this session.
        ''' Each row is stored in literal LDATE form.
        ''' </summary>
        Private ReadOnly _cacheDates As New Queue(Of PTR)()

        ''' <summary>
        ''' Maps UTC ticks to the pointer representation used in DATA for dates
        ''' that have already been promoted into the date table.
        ''' </summary>
        Private ReadOnly _datePointerByTicks As New Dictionary(Of Long, PTR)()

        ''' <summary>
        ''' Tracks dates seen once but not yet promoted into the date table.
        ''' 
        ''' Promotion rule:
        ''' - first occurrence  => inline literal
        ''' - second occurrence => add to table and start using pointers
        ''' </summary>
        Private ReadOnly _seenDateTicksOnce As New HashSet(Of Long)()

        ''' <summary>
        ''' Writes the per-session date table.
        ''' 
        ''' Wire layout:
        ''' [LUINT count][count * 8-byte LDATE rows]
        ''' 
        ''' Notes:
        ''' - The DATE TABLE zone is omitted entirely when count = 0.
        ''' </summary>
        Public Sub WriteDateCacheTable(ms As Stream)
            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _cacheDates.Count = 0 Then Return

            Writer.WriteLUINT(_cacheDates.Count, ms)
            For Each row As PTR In _cacheDates
                ms.Write(row.data)
            Next
        End Sub

        ''' <summary>
        ''' Adds a date into the session date strategy and returns the correct DATA
        ''' representation for the current occurrence.
        ''' 
        ''' Behavior:
        ''' - null            => DDATE null
        ''' - first seen      => inline DDATE literal
        ''' - second seen     => create date-table entry and return pointer
        ''' - later seen      => reuse existing pointer
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function AddDate(d As Date?) As PTR

            If Not d.HasValue Then
                Return DDATE_CHUNK_NULL_VALUE
            End If

            Dim utc As Date = Codec.NormalizeDateUtc(d.Value)
            Dim ticks As Long = utc.Ticks

            Dim p As PTR = Nothing
            If _datePointerByTicks.TryGetValue(ticks, p) Then
                Return p
            End If

            If _seenDateTicksOnce.Contains(ticks) Then

                Dim index As Integer = _cacheDates.Count

                _cacheDates.Enqueue(Codec.FromDate(utc))

                p = Codec.DatePointer(index)
                _datePointerByTicks.Add(ticks, p)

                Return p
            End If

            _seenDateTicksOnce.Add(ticks)
            Return Codec.FromDate(utc)

        End Function

#End Region

#Region "String Cache (per-session string-table)"

        ''' <summary>
        ''' Ordered string-table rows for this session.
        ''' Rows are stored in literal LSTR form.
        ''' </summary>
        Private ReadOnly _cacheStrings As New Queue(Of PTR)()

        ''' <summary>
        ''' Maps full string value to its session pointer representation.
        ''' </summary>
        Private ReadOnly _stringPointerByValue As New Dictionary(Of String, PTR)(StringComparer.Ordinal)

        ''' <summary>
        ''' Writes the per-session string table.
        ''' 
        ''' Wire layout:
        ''' [LUINT count][count * LSTR rows]
        ''' 
        ''' Notes:
        ''' - The STRING TABLE zone is omitted entirely when count = 0.
        ''' </summary>
        Public Sub WriteStringCacheTable(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _cacheStrings.Count = 0 Then Return

            Writer.WriteLUINT(Me._cacheStrings.Count, ms)
            For Each row As PTR In _cacheStrings
                ms.Write(row.len)
                ms.Write(row.data)
            Next

        End Sub

        ''' <summary>
        ''' Adds or resolves a string for this session and returns the DATA/SCHEMA
        ''' representation to be written.
        ''' 
        ''' Behavior:
        ''' - null          => DSTR null literal
        ''' - first seen    => add to string table and return DSTR pointer
        ''' - later seen    => reuse the same DSTR pointer
        ''' 
        ''' Note:
        ''' This session strategy always promotes non-null strings into the string table.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Function AddString(value As String) As PTR Implements ISession.AddString

            If value Is Nothing Then
                Return Codec.DSTRLiteralFromString(Nothing)
            End If

            Dim pointerBytes As PTR = Nothing

            If _stringPointerByValue.TryGetValue(value, pointerBytes) Then
                Return pointerBytes
            End If

            Dim index As Integer = _cacheStrings.Count

            _cacheStrings.Enqueue(Codec.LSTRFromString(value))

            pointerBytes = Codec.DSTRPointer(index)

            _stringPointerByValue.Add(value, pointerBytes)
            Return pointerBytes

        End Function

#End Region

#Region "Schema Table Writer (per-session)"

        ''' <summary>
        ''' Finalizes all session schemas, then writes the session schema table.
        ''' 
        ''' Wire layout:
        ''' [LUINT schemaCount][schema0][schema1]...[schemaN]
        ''' 
        ''' Notes:
        ''' - The SCHEMA TABLE zone is omitted entirely when no schema exists.
        ''' </summary>
        Public Sub WriteSchemaTable(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _schemaList.Count = 0 Then Return

            ' Initialization may add new dependent schemas while iterating, so this
            ' must be a growing loop rather than a simple For loop over the original size.
            Dim i As Integer = 0
            Do While i < _schemaList.Count
                InitializeSessionSchema(_schemaList(i))
                i += 1
            Loop

            Writer.WriteLUINT(_schemaList.Count, ms)

            For i = 0 To _schemaList.Count - 1
                _schemaList(i).WriteToStream(ms, Me)
            Next

        End Sub

#End Region

#Region "Values Section (encode)"

        ''' <summary>
        ''' Writes the root DATA section.
        ''' 
        ''' Root encoding supports three major families:
        ''' - root null
        ''' - root primitive / primitive array
        ''' - root object / map / array schema
        ''' </summary>
        Public Sub WriteData(obj As Object, ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            ' ROOT NULL
            If obj Is Nothing Then
                ms.WriteByte(SOBJ_NULL_TAG)
                Return
            End If

            ' ROOT PRIMITIVE / PRIMITIVE ARRAY
            Dim rootTypeCode As JsonFieldType = DotNet.Cache.TypeToFieldType(obj.GetType())
            Dim rootBaseType As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(rootTypeCode)

            If rootBaseType >= JsonFieldType.Integer AndAlso rootBaseType <= JsonFieldType.Bytes Then
                ' First byte is the root type code itself.
                ms.WriteByte(CByte(rootTypeCode))

                ' Then comes the normal payload for that root type.
                WriteValue(rootTypeCode, Nothing, obj, ms)
                Return
            End If

            ' ROOT OBJECT / MAP / ARRAY
            Dim rootDs As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(obj.GetType())
            Dim rootSs As SessionSchema = EnsureSchema(rootDs)

            ' Root complex values begin with their schema pointer.
            ms.Write(rootSs.Pointer.len)
            ms.Write(rootSs.Pointer.data)

            If rootDs.IsMap Then
                WriteMapValue(rootDs, obj, ms)
                Return
            End If

            If rootDs.IsArray Then
                WriteArraySchemaBody(rootDs, obj, ms)
                Return
            End If

            WriteObjectValue(rootDs, obj, ms,
                     writeSchemaPtr:=False,
                     schemaPtrOverride:=Nothing,
                     overrideFieldType:=JsonFieldType.Unknown)

        End Sub

        ''' <summary>
        ''' Writes an object slot using SOBJ rules.
        ''' 
        ''' Supported paths:
        ''' - null object
        ''' - primitive scalar override
        ''' - primitive array override
        ''' - object expected-schema marker
        ''' - object schema override pointer
        ''' </summary>
        Private Sub WriteObjectValue(ds As DotNet.DotNetSchema,
                             obj As Object,
                             ms As Stream,
                             Optional writeSchemaPtr As Boolean = False,
                             Optional schemaPtrOverride As (len As Byte(), data As Byte()) = Nothing,
                             Optional overrideFieldType As JsonFieldType = JsonFieldType.Unknown,
                             Optional overrideExpectedRefType As Type = Nothing)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            If obj Is Nothing Then
                ms.WriteByte(SOBJ_NULL_TAG)
                Return
            End If

            EnterRef(obj)
            Try
                ' 1) Primitive / primitive-array override.
                If overrideFieldType <> JsonFieldType.Unknown Then

                    Dim baseOv As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(overrideFieldType)
                    Dim isArrOv As Boolean = JsonField.FieldTypeIsArray(overrideFieldType)

                    If baseOv = JsonFieldType.Object AndAlso Not isArrOv Then
                        Throw New Exception("Invalid overrideFieldType: Object scalar override is not allowed.")
                    End If

                    ' Marker byte is the override type code itself.
                    ms.WriteByte(CByte(overrideFieldType))

                    If baseOv = JsonFieldType.Object Then
                        If overrideExpectedRefType Is Nothing Then
                            Throw New Exception("Object array override requires overrideExpectedRefType.")
                        End If
                        WriteValue(overrideFieldType, overrideExpectedRefType, obj, ms)
                    Else
                        WriteValue(overrideFieldType, Nothing, obj, ms)
                    End If

                    Return
                End If

                ' 2) Expected schema or schema override.
                If writeSchemaPtr Then
                    ms.Write(schemaPtrOverride.len)
                    ms.Write(schemaPtrOverride.data)
                Else
                    ms.WriteByte(SOBJ_PRESENT_EXPECTED_SCHEMA)
                End If

                ' 3) Body.
                If ds.IsMap Then
                    WriteMapValue(ds, obj, ms)
                    Return
                End If

                If ds.IsArray Then
                    WriteArraySchemaBody(ds, obj, ms)
                    Return
                End If

                Dim metas = ds.FieldsMeta
                If metas Is Nothing OrElse metas.Length = 0 Then Return

                For i As Integer = 0 To metas.Length - 1
                    Dim m = metas(i)
                    Dim v As Object = m.Getter(obj)
                    WriteValue(m.TypeCode, m.RefDotNetType, v, ms)
                Next

            Finally
                ExitRef(obj)
            End Try

        End Sub

        ''' <summary>
        ''' Writes a map body from either:
        ''' - IDictionary
        ''' - generic IDictionary(Of K,V) exposed through cached accessors
        ''' 
        ''' Map body layout:
        ''' [LUINT count][key][value][key][value]...
        ''' 
        ''' Keys are always serialized as strings.
        ''' Values are serialized according to the map schema's value type.
        ''' </summary>
        Private Sub WriteMapValue(ds As DotNet.DotNetSchema, obj As Object, ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If ds Is Nothing Then Throw New ArgumentNullException(NameOf(ds))

            If obj Is Nothing Then
                Throw New Exception("WriteMapValue called with obj = Nothing (object marker already written).")
            End If

            Dim vt As JsonFieldType = ds.MapValueTypeCode
            Dim baseVal As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(vt)

            Dim expectedRefType As Type = Nothing
            If baseVal = JsonFieldType.Object Then
                expectedRefType = ResolveMapValueRefType(ds.DotNetType)
                If expectedRefType Is Nothing Then Throw New Exception("Map value expectedRefType is missing.")
            End If

            Dim dict As IDictionary = TryCast(obj, IDictionary)

            If dict IsNot Nothing Then

                Writer.WriteLUINT(dict.Count, ms)

                For Each de As DictionaryEntry In dict

                    Dim keyChunk As PTR = AddString(If(de.Key Is Nothing, Nothing, de.Key.ToString()))
                    ms.Write(keyChunk.len)
                    ms.Write(keyChunk.data)

                    WriteValue(ds.MapValueTypeCode, expectedRefType, de.Value, ms)

                Next

                Return
            End If

            Dim ti = DotNet.Cache.GetInfo(obj.GetType())
            If ti Is Nothing OrElse Not ti.IsDictionary Then
                Throw New NotSupportedException("Map value is not IDictionary: " & obj.GetType().FullName)
            End If
            If ti.DictCountGetter Is Nothing OrElse ti.DictEntryKeyGetter Is Nothing OrElse ti.DictEntryValueGetter Is Nothing Then
                Throw New NotSupportedException("Map generic accessors missing for: " & obj.GetType().FullName)
            End If

            Dim count As Integer = ti.DictCountGetter(obj)
            Writer.WriteLUINT(count, ms)

            For Each kvpObj As Object In DirectCast(obj, IEnumerable)

                Dim keyObj As Object = ti.DictEntryKeyGetter(kvpObj)
                Dim valObj As Object = ti.DictEntryValueGetter(kvpObj)

                Dim keyChunk As PTR = AddString(If(keyObj Is Nothing, Nothing, keyObj.ToString()))
                ms.Write(keyChunk.len)
                ms.Write(keyChunk.data)

                WriteValue(ds.MapValueTypeCode, expectedRefType, valObj, ms)

            Next

        End Sub

        ''' <summary>
        ''' Dispatches a field or value based on the ArrayFlag bit.
        ''' </summary>
        Private Sub WriteValue(typeCode As JsonFieldType, expectedRefType As Type, value As Object, ms As Stream)

            Dim isArray As Boolean = JsonField.FieldTypeIsArray(typeCode)
            Dim baseType As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)

            If isArray Then
                WriteArray(baseType, expectedRefType, value, ms)
            Else
                WriteScalar(baseType, expectedRefType, value, ms)
            End If

        End Sub

        ''' <summary>
        ''' Writes an array or enumerable value.
        ''' 
        ''' Wire layout:
        ''' - null     => [LUINT_NULL]
        ''' - non-null => [LUINT count][element0][element1]...
        ''' 
        ''' Object arrays delegate each element to the object-slot writer.
        ''' Primitive arrays delegate each element to the primitive scalar writer.
        ''' </summary>
        Private Sub WriteArray(baseType As JsonFieldType, expectedRefType As Type, value As Object, ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            If value Is Nothing Then
                ms.WriteByte(LUINT_NULL)
                Return
            End If

            Dim arr As Array = TryCast(value, Array)
            If arr IsNot Nothing Then

                Writer.WriteLUINT(arr.Length, ms)

                For i As Integer = 0 To arr.Length - 1
                    Dim it As Object = arr.GetValue(i)
                    If baseType = JsonFieldType.Object Then
                        WriteScalar(JsonFieldType.Object, expectedRefType, it, ms)
                    Else
                        WriteScalar(baseType, Nothing, it, ms)
                    End If
                Next

                Return
            End If

            Dim en As IEnumerable = TryCast(value, IEnumerable)
            If en Is Nothing Then Throw New NotSupportedException("ArrayFlag set but value is not Array/IEnumerable.")

            ' Materialize when only IEnumerable is available so we can emit the count first.
            Dim tmp As New List(Of Object)()
            For Each it As Object In en
                tmp.Add(it)
            Next

            Writer.WriteLUINT(tmp.Count, ms)

            For Each it As Object In tmp
                If baseType = JsonFieldType.Object Then
                    WriteScalar(JsonFieldType.Object, expectedRefType, it, ms)
                Else
                    WriteScalar(baseType, Nothing, it, ms)
                End If
            Next

        End Sub

        ''' <summary>
        ''' Writes a scalar value according to the base field type.
        ''' </summary>
        Private Sub WriteScalar(baseType As JsonFieldType, expectedRefType As Type, value As Object, ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            Select Case baseType

                Case JsonFieldType.Integer
                    Writer.WriteLINT(value, ms)

                Case JsonFieldType.Boolean
                    Writer.WriteBool(value, ms)

                Case JsonFieldType.Float4Bytes
                    Writer.WriteSingle(value, ms)

                Case JsonFieldType.Float8Bytes
                    Writer.WriteDouble(value, ms)

                Case JsonFieldType.Date
                    If value Is Nothing Then
                        ms.Write(DDATE_CHUNK_NULL_VALUE.len)
                        ms.Write(DDATE_CHUNK_NULL_VALUE.data)
                    Else
                        Dim chunk As PTR = AddDate(CDate(value))
                        ms.Write(chunk.len)
                        ms.Write(chunk.data)
                    End If

                Case JsonFieldType.String
                    Dim chunk As PTR = AddString(If(value Is Nothing, Nothing, CStr(value)))
                    ms.Write(chunk.len)
                    ms.Write(chunk.data)

                Case JsonFieldType.Bytes
                    If value Is Nothing Then
                        ms.WriteByte(LUINT_NULL)
                    Else
                        Dim b As Byte() = DirectCast(value, Byte())
                        Dim chunk As PTR = Codec.FromBytes(b)
                        ms.Write(chunk.len)
                        ms.Write(chunk.data)
                    End If

                Case JsonFieldType.Object
                    WriteObjectSlot(expectedRefType, value, ms)

                Case Else
                    Throw New NotSupportedException("Unsupported FieldType: " & baseType.ToString())

            End Select

        End Sub

        ''' <summary>
        ''' Writes a value into an object slot using SOBJ semantics.
        ''' 
        ''' Possible outcomes:
        ''' - null object marker
        ''' - primitive scalar override
        ''' - primitive array override
        ''' - object array override
        ''' - expected-schema object
        ''' - schema-override object
        ''' </summary>
        Private Sub WriteObjectSlot(expectedRefType As Type, value As Object, ms As Stream)

            If value Is Nothing Then
                ms.WriteByte(SOBJ_NULL_TAG)
                Return
            End If

            If expectedRefType Is Nothing Then
                Throw New Exception("Object slot requires expectedRefType.")
            End If

            EnterRef(value)
            Try

                Dim rtInfo = DotNet.Cache.GetInfo(value.GetType())
                Dim rtTypeCode As JsonFieldType = If(rtInfo Is Nothing, JsonFieldType.Object, rtInfo.JsonFieldType)

                If rtTypeCode = JsonFieldType.Unknown Then
                    Throw New NotSupportedException("Runtime value has Unknown JsonFieldType: " & value.GetType().FullName)
                End If

                Dim rtIsArray As Boolean = JsonField.FieldTypeIsArray(rtTypeCode)
                Dim rtBase As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(rtTypeCode)

                ' 1) Primitive scalar / primitive array override.
                If rtBase <> JsonFieldType.Object Then

                    Dim baseId As Integer = CInt(rtBase)
                    If baseId < CInt(SOBJ_PRIMITIVE_MIN) OrElse baseId > CInt(SOBJ_PRIMITIVE_MAX) Then
                        Throw New Exception("Invalid primitive override baseId for SOBJ: " & baseId)
                    End If

                    If rtIsArray Then
                        ms.WriteByte(CByte(SOBJ_ARRAY_FLAG Or CByte(baseId)))
                        WriteArray(rtBase, Nothing, value, ms)
                    Else
                        ms.WriteByte(CByte(baseId))
                        WriteScalar(rtBase, Nothing, value, ms)
                    End If

                    Return
                End If

                ' 2) Object array override.
                If rtIsArray Then
                    ms.WriteByte(CByte(JsonFieldType.ArrayFlag Or JsonFieldType.Object))
                    WriteObjectArrayOverrideExpected(expectedRefType, value, ms)
                    Return
                End If

                ' 3) Object scalar: expected schema or schema override.
                Dim expectedDs As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(expectedRefType)
                Dim expectedKey As String = expectedDs.JsonSchemaKey

                Dim runtimeDs As DotNet.DotNetSchema = DotNet.Cache.GetDotNetSchema(value.GetType())
                Dim runtimeKey As String = runtimeDs.JsonSchemaKey

                Dim runtimeSs As SessionSchema = EnsureSchema(runtimeDs)

                Dim needOverrideSchemaPointer As Boolean =
            Not String.Equals(runtimeKey, expectedKey, StringComparison.Ordinal)

                If needOverrideSchemaPointer Then
                    ms.Write(runtimeSs.Pointer.len)
                    ms.Write(runtimeSs.Pointer.data)
                Else
                    ms.WriteByte(SOBJ_PRESENT_EXPECTED_SCHEMA)
                End If

                WriteObjectBody(runtimeDs, value, ms)

            Finally
                ExitRef(value)
            End Try

        End Sub

        ''' <summary>
        ''' Writes an override payload for Object[] when the surrounding slot already
        ''' established that the runtime value is an object array override.
        ''' </summary>
        Private Sub WriteObjectArrayOverrideExpected(expectedRefType As Type, value As Object, ms As Stream)

            If value Is Nothing Then
                ms.WriteByte(LUINT_NULL)
                Return
            End If

            Dim arr As Array = TryCast(value, Array)
            If arr IsNot Nothing Then
                Writer.WriteLUINT(arr.Length, ms)
                For i As Integer = 0 To arr.Length - 1
                    WriteObjectSlot(expectedRefType, arr.GetValue(i), ms)
                Next
                Return
            End If

            Dim en As IEnumerable = TryCast(value, IEnumerable)
            If en Is Nothing Then Throw New NotSupportedException("Object[] override but value is not Array/IEnumerable.")

            Dim tmp As New List(Of Object)()
            For Each it As Object In en
                tmp.Add(it)
            Next

            Writer.WriteLUINT(tmp.Count, ms)
            For Each it As Object In tmp
                WriteObjectSlot(expectedRefType, it, ms)
            Next

        End Sub

        ''' <summary>
        ''' Writes only the body of an object, map, or array schema after the proper
        ''' marker / schema pointer has already been emitted.
        ''' </summary>
        Private Sub WriteObjectBody(ds As DotNet.DotNetSchema, obj As Object, ms As Stream)

            If ds Is Nothing Then Throw New ArgumentNullException(NameOf(ds))

            If ds.IsMap Then
                WriteMapValue(ds, obj, ms)
                Return
            End If

            If ds.IsArray Then
                WriteArraySchemaBody(ds, obj, ms)
                Return
            End If

            Dim metas = ds.FieldsMeta
            If metas Is Nothing OrElse metas.Length = 0 Then Return

            For i As Integer = 0 To metas.Length - 1
                Dim m = metas(i)
                Dim v As Object = m.Getter(obj)
                WriteValue(m.TypeCode, m.RefDotNetType, v, ms)
            Next

        End Sub

        ''' <summary>
        ''' Resolves the declared element type for an array or enumerable container type.
        ''' </summary>
        Private Shared Function ResolveArrayElemDeclaredType(containerType As Type) As Type
            If containerType Is Nothing Then Return Nothing
            If containerType.IsArray Then Return containerType.GetElementType()
            Dim info = DotNet.Cache.GetInfo(containerType)
            Return If(info Is Nothing, Nothing, info.EnumerableElement)
        End Function

        ''' <summary>
        ''' Resolves the schema owner type for array elements whose base type is Object.
        ''' 
        ''' Rules:
        ''' - nested arrays keep their own declared type
        ''' - enumerable element containers keep their declared type
        ''' - regular object references are normalized by ResolveObjectSchemaType
        ''' </summary>
        Private Shared Function ResolveArrayElemExpectedSchemaType(elemDeclared As Type) As Type
            If elemDeclared Is Nothing Then Return Nothing

            If elemDeclared.IsArray Then Return elemDeclared

            Dim ei = DotNet.Cache.GetInfo(elemDeclared)
            If ei IsNot Nothing AndAlso ei.EnumerableElement IsNot Nothing AndAlso
       elemDeclared IsNot GetType(String) AndAlso elemDeclared IsNot GetType(Byte()) Then
                Return elemDeclared
            End If

            Return ResolveObjectSchemaType(elemDeclared)
        End Function

        ''' <summary>
        ''' Writes the body for a schema whose root kind is Array.
        ''' 
        ''' For array schemas, the schema already says "this is a collection", so
        ''' ArrayValueType must be the element base type without ArrayFlag.
        ''' </summary>
        Private Sub WriteArraySchemaBody(ds As DotNet.DotNetSchema, value As Object, ms As Stream)

            Dim elemTc As JsonFieldType = ds.ArrayValueTypeCode
            If JsonField.FieldTypeIsArray(elemTc) Then
                Throw New Exception("Array schema elemType cannot have ArrayFlag: " & elemTc.ToString())
            End If

            Dim expectedRefType As Type = Nothing
            If elemTc = JsonFieldType.Object Then
                Dim elemDeclared As Type = ResolveArrayElemDeclaredType(ds.DotNetType)
                expectedRefType = ResolveArrayElemExpectedSchemaType(elemDeclared)
                If expectedRefType Is Nothing Then Throw New Exception("Array schema expectedRefType is missing.")
            End If

            WriteArray(elemTc, expectedRefType, value, ms)

        End Sub

#End Region

#Region "Files Zone (optional per-session file area)"

        ''' <summary>
        ''' Ordered file entries for this session.
        ''' 
        ''' Notes:
        ''' - File names are unique within the session.
        ''' - The zone is optional and omitted entirely when it has zero entries.
        ''' </summary>
        Private ReadOnly _filesList As New List(Of KeyValuePair(Of String, Byte()))()

        ''' <summary>
        ''' Maps file name to its position in <see cref="_filesList"/> so updates
        ''' can replace the existing entry without changing insertion order.
        ''' </summary>
        Private ReadOnly _fileIndexByName As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

        ''' <summary>
        ''' Creates a new encode session with an optional initial file set.
        ''' </summary>
        Public Sub New(Optional files As IDictionary(Of String, Byte()) = Nothing)
            SetFiles(files)
        End Sub

        ''' <summary>
        ''' Replaces the current per-session file set.
        ''' 
        ''' The FILES zone is omitted when this set is empty.
        ''' </summary>
        Public Sub SetFiles(files As IDictionary(Of String, Byte()))
            _filesList.Clear()
            _fileIndexByName.Clear()

            If files Is Nothing Then Return

            For Each kv As KeyValuePair(Of String, Byte()) In files
                AddFile(kv.Key, kv.Value)
            Next
        End Sub

        ''' <summary>
        ''' Adds or replaces a file entry in the per-session FILES zone.
        ''' </summary>
        Public Sub AddFile(fileName As String, fileData As Byte())

            If String.IsNullOrWhiteSpace(fileName) Then
                Throw New ArgumentNullException(NameOf(fileName))
            End If

            If fileData Is Nothing Then
                Throw New ArgumentNullException(NameOf(fileData))
            End If

            Dim e As New KeyValuePair(Of String, Byte())(fileName, fileData)

            Dim idx As Integer
            If _fileIndexByName.TryGetValue(fileName, idx) Then
                _filesList(idx) = e
            Else
                idx = _filesList.Count
                _filesList.Add(e)
                _fileIndexByName.Add(fileName, idx)
            End If

        End Sub

        ''' <summary>
        ''' Returns whether this session will emit a FILES zone.
        ''' </summary>
        Friend ReadOnly Property HasFiles As Boolean
            Get
                Return _filesList.Count <> 0
            End Get
        End Property

        ''' <summary>
        ''' Writes the optional FILES zone.
        ''' 
        ''' Canonical zone position:
        ''' after HEADER and before STRING TABLE / DATE TABLE / SCHEMA TABLE / DATA.
        ''' 
        ''' Wire layout:
        ''' [LUINT filesByteLength][LUINT fileCount][file0][file1]...[fileN]
        ''' 
        ''' Each file entry is encoded as:
        ''' [LSTR literal fileName][LUINT byteLength][raw bytes]
        ''' 
        ''' Notes:
        ''' - File names are intentionally written as literal LSTR so the FILES
        '''   zone remains self-contained and independent from StringTable policy.
        ''' - The FILES zone is omitted entirely when it has zero entries.
        ''' - filesByteLength covers the complete serialized FILES payload
        '''   that follows it, including the LUINT fileCount and all file entries.
        ''' </summary>
        Public Sub WriteFilesZone(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _filesList.Count = 0 Then Return

            Dim count As Integer = _filesList.Count
            Dim filesPayloadLength As Long = Writer.GetLUINTEncodedByteCount(CLng(count))

            ' Pre-pass:
            '   Compute the exact FILES payload length without allocating an
            '   intermediate stream. The payload is:
            '   [fileCount][file0][file1]...[fileN]
            For i As Integer = 0 To count - 1

                Dim e = _filesList(i)

                filesPayloadLength += Writer.GetLSTREncodedByteCount(e.Key)
                filesPayloadLength += Writer.GetBarrEncodedByteCount(e.Value)

            Next

            ' Zone prefix:
            '   [filesByteLength][fileCount]
            Writer.WriteLUINT(filesPayloadLength, ms)
            Writer.WriteLUINT(CLng(count), ms)

            ' Body:
            '   [fileName:LSTR][filePayload:BARR]...
            For i As Integer = 0 To count - 1

                Dim e = _filesList(i)

                WriteHeaderLstrLiteral(ms, e.Key)

                Dim p As PTR = Codec.FromBytes(e.Value)
                ms.Write(p.len)
                ms.Write(p.data)

            Next

        End Sub

#End Region

#Region "Session Lifecycle"

        Public Sub Clear()
            _activeRefs.Clear()

            _hasHeader = False
            _headerList.Clear()
            _headerIndexByKey.Clear()

            _cacheStrings.Clear()
            _stringPointerByValue.Clear()

            _cacheDates.Clear()
            _datePointerByTicks.Clear()
            _seenDateTicksOnce.Clear()

            _schemaList.Clear()
            _schemaByKey.Clear()
            _dotNetByJsonKey.Clear()

            _initialized.Clear()
            _initializing.Clear()
            _expandingDependencies.Clear()
            _expandedDependencies.Clear()

            _filesList.Clear()
            _fileIndexByName.Clear()

        End Sub
#End Region

    End Class

End Namespace