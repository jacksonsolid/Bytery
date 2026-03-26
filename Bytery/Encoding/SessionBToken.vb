Imports System.IO
Imports System.Runtime.CompilerServices
Imports Bytery.JSON
Imports Bytery.Linq

Namespace Encoding

    Friend NotInheritable Class SessionBToken
        Implements ISession

#Region "Session Schema Table (LINQ)"

        Private ReadOnly _schemaList As New List(Of SessionSchema)()
        Private ReadOnly _schemaByKey As New Dictionary(Of String, SessionSchema)(StringComparer.Ordinal)
        Private ReadOnly _initialized As New HashSet(Of String)(StringComparer.Ordinal)

        Private _genericObjectSchema As SessionSchema = Nothing

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function EnsureSchema(js As JsonSchema) As SessionSchema
            If js Is Nothing Then Throw New ArgumentNullException(NameOf(js))
            If String.IsNullOrWhiteSpace(js.Key) Then Throw New Exception("JsonSchema.Key cannot be null/empty.")

            Dim existing As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(js.Key, existing) Then Return existing

            Dim idx As Integer = _schemaList.Count
            Dim ptr As PTR = Codec.SchemaPointer(idx)

            Dim ss As New SessionSchema(js) With {
                .Index = idx,
                .Pointer = ptr
            }

            _schemaList.Add(ss)
            _schemaByKey.Add(js.Key, ss)

            Return ss
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function EnsureSchemaByKey(key As String) As SessionSchema
            If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentNullException(NameOf(key))

            Dim ss As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(key, ss) Then Return ss

            Throw New Exception("Missing session schema for key: " & key)
        End Function

        Private Sub InitializeSessionSchema(ss As SessionSchema)

            If ss Is Nothing OrElse ss.JS Is Nothing Then Return

            Dim key As String = ss.JS.Key
            If _initialized.Contains(key) Then Return

            Select Case ss.JS.Kind

                Case JsonSchema.SchemaKind.Map

                    Dim vt As JsonFieldType = ss.JS.DictValueType
                    Dim baseVal As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(vt)

                    If baseVal < JsonFieldType.Integer OrElse baseVal > JsonFieldType.Object Then
                        Throw New Exception("Invalid map DictValueType base for schema: " & ss.JS.Key & " => " & vt.ToString())
                    End If

                    If baseVal = JsonFieldType.Object Then
                        If String.IsNullOrWhiteSpace(ss.JS.DictValueSchemaKey) Then
                            Throw New Exception("Map value is Object/Object[] but DictValueSchemaKey is missing: " & ss.JS.Key)
                        End If

                        Dim refSs As SessionSchema = EnsureSchemaByKey(ss.JS.DictValueSchemaKey)
                        ss.DictValueSchemaPtr = refSs.Pointer
                    End If

                    ss.Fields = Array.Empty(Of SessionField)()
                    _initialized.Add(key)
                    Return

                Case JsonSchema.SchemaKind.Array

                    Dim et As JsonFieldType = ss.JS.ArrayValueType

                    If JsonField.FieldTypeIsArray(et) Then
                        Throw New Exception("Array schema ArrayValueType cannot have ArrayFlag: " & et.ToString())
                    End If

                    Dim baseElem As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(et)

                    If baseElem = JsonFieldType.Object Then
                        If String.IsNullOrWhiteSpace(ss.JS.ArrayValueSchemaKey) Then
                            Throw New Exception("Array element is Object but ArrayValueSchemaKey is missing: " & ss.JS.Key)
                        End If

                        Dim refSs As SessionSchema = EnsureSchemaByKey(ss.JS.ArrayValueSchemaKey)
                        ss.ArrayValueSchemaPtr = refSs.Pointer
                    End If

                    ss.Fields = Array.Empty(Of SessionField)()
                    _initialized.Add(key)
                    Return

                Case Else

                    Dim jf As JsonField() = ss.JS.Fields
                    Dim count As Integer = If(jf Is Nothing, 0, jf.Length)

                    If count = 0 Then
                        ss.Fields = Array.Empty(Of SessionField)()
                        _initialized.Add(key)
                        Return
                    End If

                    Dim out(count - 1) As SessionField

                    For i As Integer = 0 To count - 1
                        Dim f As JsonField = jf(i)
                        If f Is Nothing Then Throw New Exception("JsonSchema.Fields contains null entry: " & ss.JS.Key)

                        Dim sf As New SessionField(f)

                        If JsonField.FieldTypeIsObjectOrObjectArray(f.TypeCode) Then
                            If String.IsNullOrWhiteSpace(f.RefSchemaKey) Then
                                Throw New Exception("Object/Object[] field without RefSchemaKey: " & f.Name & " (schema=" & ss.JS.Key & ")")
                            End If

                            Dim refSs As SessionSchema = EnsureSchemaByKey(f.RefSchemaKey)
                            sf.RefSchemaPtr = refSs.Pointer
                        End If

                        out(i) = sf
                    Next

                    ss.Fields = out
                    _initialized.Add(key)
                    Return

            End Select

        End Sub

        Private Function EnsureGenericObjectSchema() As SessionSchema

            If _genericObjectSchema IsNot Nothing Then
                Return _genericObjectSchema
            End If

            Dim js As New JsonSchema(
                "LINQ:OBJ:{}",
                Array.Empty(Of JsonField)()
            )

            _genericObjectSchema = EnsureSchema(js)
            InitializeSessionSchema(_genericObjectSchema)

            Return _genericObjectSchema

        End Function

        Private Function EnsureObjectSchema(obj As BObject) As SessionSchema
            If obj Is Nothing Then Throw New ArgumentNullException(NameOf(obj))

            EnterRef(obj)
            Try
                If obj.ObjectKind = BObject.Enum_ObjectKind.Map Then
                    Return EnsureMapSchema(obj)
                End If

                Return EnsurePlainObjectSchema(obj)
            Finally
                ExitRef(obj)
            End Try
        End Function

        Private Function EnsurePlainObjectSchema(obj As BObject) As SessionSchema

            Dim fields As New List(Of JsonField)()

            For Each kv As KeyValuePair(Of String, BToken) In obj

                Dim typeCode As JsonFieldType = GetFieldTypeCode(kv.Value)
                Dim refSchemaKey As String = Nothing

                If JsonField.FieldTypeIsObjectOrObjectArray(typeCode) Then
                    refSchemaKey = GetRefSchemaKeyForField(kv.Value, typeCode)
                End If

                fields.Add(New JsonField With {
                    .Name = kv.Key,
                    .TypeCode = typeCode,
                    .RefSchemaKey = refSchemaKey
                })

            Next

            Dim key As String = BuildPlainObjectSchemaKey(fields)

            Dim existing As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(key, existing) Then Return existing

            Dim js As New JsonSchema(key, fields.ToArray())

            Dim ss As SessionSchema = EnsureSchema(js)
            InitializeSessionSchema(ss)

            Return ss

        End Function

        Private Function EnsureMapSchema(mapObj As BObject) As SessionSchema

            Dim valueType As JsonFieldType = InferMapValueTypeCode(mapObj)
            Dim refSchemaKey As String = Nothing

            If JsonField.FieldTypeIsObjectOrObjectArray(valueType) Then
                refSchemaKey = GetRefSchemaKeyForMap(mapObj, valueType)
            End If

            Dim key As String =
                "LINQ:MAP:" &
                CInt(valueType).ToString(Globalization.CultureInfo.InvariantCulture) & ":" &
                If(refSchemaKey, "")

            Dim existing As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(key, existing) Then Return existing

            Dim js As New JsonSchema(
                key,
                JsonSchema.SchemaKind.Map,
                valueType,
                refSchemaKey
            )

            Dim ss As SessionSchema = EnsureSchema(js)
            InitializeSessionSchema(ss)

            Return ss

        End Function

        Private Function EnsureRootArraySchema(arr As BArray) As SessionSchema

            Dim elemType As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(InferArrayTypeCode(arr))
            If elemType <> JsonFieldType.Object Then
                Throw New Exception("EnsureRootArraySchema should only be used for Object[]/mixed arrays.")
            End If

            Dim refSchemaKey As String = DetermineExpectedSchemaKeyForObjectArray(arr)
            Dim key As String = "LINQ:ROOTARR:" & If(refSchemaKey, "")

            Dim existing As SessionSchema = Nothing
            If _schemaByKey.TryGetValue(key, existing) Then Return existing

            Dim js As New JsonSchema(
                key,
                JsonSchema.SchemaKind.Array,
                JsonFieldType.Object,
                refSchemaKey
            )

            Dim ss As SessionSchema = EnsureSchema(js)
            InitializeSessionSchema(ss)

            Return ss

        End Function

        Private Shared Function BuildPlainObjectSchemaKey(fields As List(Of JsonField)) As String

            Dim sb As New Text.StringBuilder()
            sb.Append("LINQ:OBJ:")

            For i As Integer = 0 To fields.Count - 1
                If i > 0 Then sb.Append("|"c)

                Dim f As JsonField = fields(i)
                sb.Append(f.Name)
                sb.Append(":"c)
                sb.Append(CInt(f.TypeCode).ToString(Globalization.CultureInfo.InvariantCulture))

                If JsonField.FieldTypeIsObjectOrObjectArray(f.TypeCode) Then
                    sb.Append(">"c)
                    sb.Append(If(f.RefSchemaKey, ""))
                End If
            Next

            Return sb.ToString()

        End Function

        Private Function GetRefSchemaKeyForField(token As BToken, typeCode As JsonFieldType) As String

            If JsonField.FieldTypeWithoutArrayFlag(typeCode) <> JsonFieldType.Object Then
                Return Nothing
            End If

            If JsonField.FieldTypeIsArray(typeCode) Then

                Dim arr As BArray = TryCast(token, BArray)
                If arr Is Nothing Then
                    Return EnsureGenericObjectSchema().JS.Key
                End If

                Return DetermineExpectedSchemaKeyForObjectArray(arr)

            End If

            Dim obj As BObject = TryCast(token, BObject)
            If obj IsNot Nothing Then
                Return EnsureObjectSchema(obj).JS.Key
            End If

            Return EnsureGenericObjectSchema().JS.Key

        End Function

        Private Function GetRefSchemaKeyForMap(mapObj As BObject, valueType As JsonFieldType) As String

            If JsonField.FieldTypeWithoutArrayFlag(valueType) <> JsonFieldType.Object Then
                Return Nothing
            End If

            If JsonField.FieldTypeIsArray(valueType) Then

                Dim firstKey As String = Nothing

                For Each kv As KeyValuePair(Of String, BToken) In mapObj

                    If kv.Value Is Nothing OrElse kv.Value.IsNull Then Continue For

                    Dim arr As BArray = TryCast(kv.Value, BArray)
                    If arr Is Nothing Then Continue For

                    Dim arrKey As String = DetermineExpectedSchemaKeyForObjectArray(arr)

                    If firstKey Is Nothing Then
                        firstKey = arrKey
                    ElseIf Not String.Equals(firstKey, arrKey, StringComparison.Ordinal) Then
                        Return EnsureGenericObjectSchema().JS.Key
                    End If

                Next

                Return If(firstKey, EnsureGenericObjectSchema().JS.Key)

            End If

            Dim firstObjectKey As String = Nothing

            For Each kv As KeyValuePair(Of String, BToken) In mapObj

                If kv.Value Is Nothing OrElse kv.Value.IsNull Then Continue For

                Dim child As BObject = TryCast(kv.Value, BObject)
                If child Is Nothing Then Continue For

                Dim childKey As String = EnsureObjectSchema(child).JS.Key

                If firstObjectKey Is Nothing Then
                    firstObjectKey = childKey
                ElseIf Not String.Equals(firstObjectKey, childKey, StringComparison.Ordinal) Then
                    Return EnsureGenericObjectSchema().JS.Key
                End If

            Next

            Return If(firstObjectKey, EnsureGenericObjectSchema().JS.Key)

        End Function

        Private Function DetermineExpectedSchemaKeyForObjectArray(arr As BArray) As String

            If arr Is Nothing Then
                Return EnsureGenericObjectSchema().JS.Key
            End If

            EnterRef(arr)
            Try

                Dim firstKey As String = Nothing

                For Each item As BToken In arr

                    If item Is Nothing OrElse item.IsNull Then
                        Continue For
                    End If

                    Dim childObj As BObject = TryCast(item, BObject)
                    If childObj Is Nothing Then
                        Continue For
                    End If

                    Dim childKey As String = EnsureObjectSchema(childObj).JS.Key

                    If firstKey Is Nothing Then
                        firstKey = childKey
                    ElseIf Not String.Equals(firstKey, childKey, StringComparison.Ordinal) Then
                        Return EnsureGenericObjectSchema().JS.Key
                    End If

                Next

                Return If(firstKey, EnsureGenericObjectSchema().JS.Key)

            Finally
                ExitRef(arr)
            End Try

        End Function

        Private Function GetFieldTypeCode(token As BToken) As JsonFieldType

            If token Is Nothing Then
                Return JsonFieldType.Object
            End If

            If TypeOf token Is BObject Then
                Return JsonFieldType.Object
            End If

            If TypeOf token Is BArray Then
                Return InferArrayTypeCode(DirectCast(token, BArray))
            End If

            If token.IsNull Then

                If token.FieldCode = JsonFieldType.Unknown Then
                    Return JsonFieldType.Object
                End If

                If JsonField.FieldTypeIsArray(token.FieldCode) Then
                    Return token.FieldCode
                End If

                If JsonField.FieldTypeWithoutArrayFlag(token.FieldCode) = JsonFieldType.Object Then
                    Return JsonFieldType.Object
                End If

                Return token.FieldCode

            End If

            Return token.FieldCode

        End Function

        Private Function InferMapValueTypeCode(mapObj As BObject) As JsonFieldType

            If mapObj.MapValueFieldCode <> JsonFieldType.Unknown Then

                If JsonField.FieldTypeIsArray(mapObj.MapValueFieldCode) Then
                    Return mapObj.MapValueFieldCode
                End If

                Return JsonField.FieldTypeWithoutArrayFlag(mapObj.MapValueFieldCode)

            End If

            Dim first As JsonFieldType = JsonFieldType.Unknown

            For Each kv As KeyValuePair(Of String, BToken) In mapObj

                Dim tc As JsonFieldType = GetFieldTypeCode(kv.Value)
                If tc = JsonFieldType.Unknown Then Continue For

                If first = JsonFieldType.Unknown Then
                    first = tc
                ElseIf first <> tc Then
                    Return JsonFieldType.Object
                End If

            Next

            If first = JsonFieldType.Unknown Then
                Return JsonFieldType.Object
            End If

            Return first

        End Function

        Private Function InferArrayTypeCode(arr As BArray) As JsonFieldType

            If arr Is Nothing Then
                Return JsonFieldType.Object Or JsonFieldType.ArrayFlag
            End If

            If arr.FieldCode <> JsonFieldType.Unknown Then

                If JsonField.FieldTypeIsArray(arr.FieldCode) Then
                    Return arr.FieldCode
                End If

                Return JsonField.FieldTypeWithoutArrayFlag(arr.FieldCode) Or JsonFieldType.ArrayFlag

            End If

            Dim baseType As JsonFieldType = JsonFieldType.Unknown

            For Each item As BToken In arr

                Dim currentBase As JsonFieldType = GetArrayElementBaseType(item)
                If currentBase = JsonFieldType.Unknown Then Continue For

                If baseType = JsonFieldType.Unknown Then
                    baseType = currentBase
                ElseIf baseType <> currentBase Then
                    baseType = JsonFieldType.Object
                    Exit For
                End If

            Next

            If baseType = JsonFieldType.Unknown Then
                baseType = JsonFieldType.Object
            End If

            Return baseType Or JsonFieldType.ArrayFlag

        End Function

        Private Function GetArrayElementBaseType(token As BToken) As JsonFieldType

            If token Is Nothing Then
                Return JsonFieldType.Unknown
            End If

            If TypeOf token Is BArray OrElse TypeOf token Is BObject Then
                Return JsonFieldType.Object
            End If

            Dim tc As JsonFieldType = token.FieldCode

            If tc = JsonFieldType.Unknown Then
                Return JsonFieldType.Unknown
            End If

            If JsonField.FieldTypeIsArray(tc) Then
                Return JsonFieldType.Object
            End If

            Return JsonField.FieldTypeWithoutArrayFlag(tc)

        End Function

#End Region

#Region "Cyclic Reference Protection"

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

        Private ReadOnly _activeRefs As New HashSet(Of Object)(RefEqComparer.Instance)

        Private Shared Function NeedsRefTrack(value As Object) As Boolean
            Return TypeOf value Is BObject OrElse TypeOf value Is BArray
        End Function

        Private Sub EnterRef(value As Object)
            If Not NeedsRefTrack(value) Then Return
            If _activeRefs.Contains(value) Then
                Throw New InvalidOperationException($"Referência cíclica detectada: {value.GetType().FullName}")
            End If
            _activeRefs.Add(value)
        End Sub

        Private Sub ExitRef(value As Object)
            If Not NeedsRefTrack(value) Then Return
            _activeRefs.Remove(value)
        End Sub

#End Region

#Region "Container Zones (ZMSK presence byte)"

        Friend ReadOnly Property HasStringTable As Boolean
            Get
                Return _cacheStrings.Count <> 0
            End Get
        End Property

        Friend ReadOnly Property HasDateTable As Boolean
            Get
                Return _cacheDates.Count <> 0
            End Get
        End Property

        Friend ReadOnly Property HasSchemaTable As Boolean
            Get
                Return _schemaList.Count <> 0
            End Get
        End Property

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

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Sub WriteZoneMask(ms As Stream, Optional includeData As Boolean = True)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            ms.WriteByte(GetZoneMask(includeData))

        End Sub

#End Region

#Region "Header Cache (file header pairs)"

        Private _hasHeader As Boolean = False
        Private ReadOnly _headerList As New List(Of HeaderEntry)()
        Private ReadOnly _headerIndexByKey As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

        Public Sub EnableHeader()
            _hasHeader = True
        End Sub

        Friend ReadOnly Property HasHeader As Boolean
            Get
                Return _hasHeader
            End Get
        End Property

        Public Sub AddHeader(key As String, typeCode As JsonFieldType, value As Object)

            If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentNullException(NameOf(key))

            Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)
            Dim isArr As Boolean = JsonField.FieldTypeIsArray(typeCode)

            If baseTc < JsonFieldType.Integer OrElse baseTc > JsonFieldType.Bytes Then
                Throw New NotSupportedException("Header typeCode not supported: " & typeCode.ToString())
            End If

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

        Public Sub WriteHeader(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If Not _hasHeader Then Return

            Dim count As Integer = _headerList.Count

            Using body As New MemoryStream()

                ' The header payload is:
                '   [pairCount][header entries...]
                ' so headerByteLength includes the serialized LUINT pairCount itself.
                Writer.WriteLUINT(CLng(count), body)

                For i As Integer = 0 To count - 1
                    Dim e = _headerList(i)

                    WriteHeaderLstrLiteral(body, e.Key)
                    body.WriteByte(CByte(e.TypeCode))
                    WriteHeaderValue(body, e.TypeCode, e.Value)
                Next

                ' Zone body:
                '   [headerByteLength][pairCount][header entries...]
                Writer.WriteLUINT(CLng(body.Length), ms)

                body.Position = 0
                body.CopyTo(ms)

            End Using

        End Sub

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Shared Sub WriteHeaderLstrLiteral(ms As Stream, s As String)
            Dim p As PTR = Codec.LSTRFromString(s)
            ms.Write(p.len)
            ms.Write(p.data)
        End Sub

        Private Shared Sub WriteHeaderValue(ms As Stream, typeCode As JsonFieldType, value As Object)

            Dim isArr As Boolean = JsonField.FieldTypeIsArray(typeCode)
            Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)

            If isArr Then
                WriteHeaderArray(ms, baseTc, value)
            Else
                WriteHeaderScalar(ms, baseTc, value)
            End If

        End Sub

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

        Private Shared Sub WriteHeaderArray(ms As Stream, baseType As JsonFieldType, value As Object)

            If value Is Nothing Then
                ms.WriteByte(LUINT_NULL)
                Return
            End If

            Dim arr As Array = TryCast(value, Array)
            If arr Is Nothing Then
                Dim en As System.Collections.IEnumerable = TryCast(value, System.Collections.IEnumerable)
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

        Private ReadOnly _cacheDates As New Queue(Of PTR)()
        Private ReadOnly _datePointerByTicks As New Dictionary(Of Long, PTR)()
        Private ReadOnly _seenDateTicksOnce As New HashSet(Of Long)()

        Public Sub WriteDateCacheTable(ms As Stream)
            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _cacheDates.Count = 0 Then Return

            Writer.WriteLUINT(_cacheDates.Count, ms)
            For Each row As PTR In _cacheDates
                ms.Write(row.data)
            Next
        End Sub

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

        Private ReadOnly _cacheStrings As New Queue(Of PTR)()
        Private ReadOnly _stringPointerByValue As New Dictionary(Of String, PTR)(StringComparer.Ordinal)

        Public Sub WriteStringCacheTable(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _cacheStrings.Count = 0 Then Return

            Writer.WriteLUINT(_cacheStrings.Count, ms)
            For Each row As PTR In _cacheStrings
                ms.Write(row.len)
                ms.Write(row.data)
            Next

        End Sub

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

        Public Sub WriteSchemaTable(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _schemaList.Count = 0 Then Return

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

#Region "Values Section (LINQ)"

        Public Sub WriteData(root As BToken, ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))

            If root Is Nothing Then
                ms.WriteByte(SOBJ_NULL_TAG)
                Return
            End If

            If TypeOf root Is BObject Then

                Dim rootObj As BObject = DirectCast(root, BObject)
                Dim rootSs As SessionSchema = EnsureObjectSchema(rootObj)

                ms.Write(rootSs.Pointer.len)
                ms.Write(rootSs.Pointer.data)

                EnterRef(rootObj)
                Try
                    If rootObj.ObjectKind = BObject.Enum_ObjectKind.Map Then
                        WriteMapBody(rootObj, rootSs.JS.DictValueType, rootSs.JS.DictValueSchemaKey, ms)
                    Else
                        ms.WriteByte(SOBJ_PRESENT_EXPECTED_SCHEMA)
                        WritePlainObjectBody(rootObj, rootSs.JS.Fields, ms)
                    End If
                Finally
                    ExitRef(rootObj)
                End Try

                Return
            End If

            If TypeOf root Is BArray Then

                Dim rootArr As BArray = DirectCast(root, BArray)
                Dim tc As JsonFieldType = InferArrayTypeCode(rootArr)
                Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(tc)

                If baseTc <> JsonFieldType.Object Then

                    EnterRef(rootArr)
                    Try
                        ms.WriteByte(CByte(tc))
                        WriteValue(tc, Nothing, rootArr, ms)
                    Finally
                        ExitRef(rootArr)
                    End Try

                Else

                    Dim rootSs As SessionSchema = EnsureRootArraySchema(rootArr)

                    EnterRef(rootArr)
                    Try
                        ms.Write(rootSs.Pointer.len)
                        ms.Write(rootSs.Pointer.data)
                        WriteArray(JsonFieldType.Object, rootSs.JS.ArrayValueSchemaKey, rootArr, ms)
                    Finally
                        ExitRef(rootArr)
                    End Try

                End If

                Return
            End If

            Dim rootTypeCode As JsonFieldType = GetFieldTypeCode(root)
            Dim rootBase As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(rootTypeCode)

            If rootBase = JsonFieldType.Object Then
                ms.WriteByte(SOBJ_NULL_TAG)
                Return
            End If

            ms.WriteByte(CByte(rootTypeCode))
            WriteValue(rootTypeCode, Nothing, root, ms)

        End Sub

        Private Sub WritePlainObjectBody(obj As BObject, fields As JsonField(), ms As Stream)

            If fields Is Nothing OrElse fields.Length = 0 Then
                Return
            End If

            For i As Integer = 0 To fields.Length - 1
                Dim f As JsonField = fields(i)
                Dim token As BToken = obj(f.Name)
                WriteValue(f.TypeCode, f.RefSchemaKey, token, ms)
            Next

        End Sub

        Private Sub WriteMapBody(mapObj As BObject,
                                 valueType As JsonFieldType,
                                 valueRefSchemaKey As String,
                                 ms As Stream)

            Writer.WriteLUINT(CLng(mapObj.Count), ms)

            For Each kv As KeyValuePair(Of String, BToken) In mapObj

                Dim keyChunk As PTR = AddString(kv.Key)
                ms.Write(keyChunk.len)
                ms.Write(keyChunk.data)

                WriteValue(valueType, valueRefSchemaKey, kv.Value, ms)

            Next

        End Sub

        Private Sub WriteValue(typeCode As JsonFieldType,
                               expectedRefSchemaKey As String,
                               token As BToken,
                               ms As Stream)

            Dim isArray As Boolean = JsonField.FieldTypeIsArray(typeCode)
            Dim baseType As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(typeCode)

            If isArray Then
                WriteArray(baseType, expectedRefSchemaKey, TryCast(token, BArray), ms)
            Else
                WriteScalar(baseType, expectedRefSchemaKey, token, ms)
            End If

        End Sub

        Private Sub WriteArray(baseType As JsonFieldType,
                               expectedRefSchemaKey As String,
                               arr As BArray,
                               ms As Stream)

            If arr Is Nothing Then
                ms.WriteByte(LUINT_NULL)
                Return
            End If

            Writer.WriteLUINT(CLng(arr.Count), ms)

            Select Case baseType

                Case JsonFieldType.Integer,
                     JsonFieldType.Float4Bytes,
                     JsonFieldType.Float8Bytes,
                     JsonFieldType.Boolean,
                     JsonFieldType.Date,
                     JsonFieldType.String,
                     JsonFieldType.Bytes

                    For Each item As BToken In arr
                        WriteScalar(baseType, Nothing, item, ms)
                    Next

                Case JsonFieldType.Object

                    Dim expectedKey As String = If(expectedRefSchemaKey, EnsureGenericObjectSchema().JS.Key)

                    For Each item As BToken In arr
                        WriteObjectSlot(expectedKey, item, ms)
                    Next

                Case Else
                    Throw New NotSupportedException("Unsupported array base type: " & baseType.ToString())

            End Select

        End Sub

        Private Sub WriteScalar(baseType As JsonFieldType,
                                expectedRefSchemaKey As String,
                                token As BToken,
                                ms As Stream)

            Select Case baseType

                Case JsonFieldType.Integer
                    Writer.WriteLINT(If(token Is Nothing OrElse token.IsNull, Nothing, token.ToObject()), ms)

                Case JsonFieldType.Boolean
                    Writer.WriteBool(If(token Is Nothing OrElse token.IsNull, Nothing, token.ToObject()), ms)

                Case JsonFieldType.Float4Bytes
                    Writer.WriteSingle(If(token Is Nothing OrElse token.IsNull, Nothing, token.ToObject()), ms)

                Case JsonFieldType.Float8Bytes
                    Writer.WriteDouble(If(token Is Nothing OrElse token.IsNull, Nothing, token.ToObject()), ms)

                Case JsonFieldType.Date
                    If token Is Nothing OrElse token.IsNull Then
                        ms.Write(DDATE_CHUNK_NULL_VALUE.len)
                        ms.Write(DDATE_CHUNK_NULL_VALUE.data)
                    Else
                        Dim chunk As PTR = AddDate(CDate(token.ToObject()))
                        ms.Write(chunk.len)
                        ms.Write(chunk.data)
                    End If

                Case JsonFieldType.String
                    Dim s As String = If(token Is Nothing OrElse token.IsNull, Nothing, CStr(token.ToObject()))
                    Dim sChunk As PTR = AddString(s)
                    ms.Write(sChunk.len)
                    ms.Write(sChunk.data)

                Case JsonFieldType.Bytes
                    If token Is Nothing OrElse token.IsNull Then
                        ms.WriteByte(LUINT_NULL)
                    Else
                        Dim b As Byte() = DirectCast(token.ToObject(), Byte())
                        Dim bChunk As PTR = Codec.FromBytes(b)
                        ms.Write(bChunk.len)
                        ms.Write(bChunk.data)
                    End If

                Case JsonFieldType.Object
                    WriteObjectSlot(expectedRefSchemaKey, token, ms)

                Case Else
                    Throw New NotSupportedException("Unsupported scalar base type: " & baseType.ToString())

            End Select

        End Sub

        Private Sub WriteObjectSlot(expectedRefSchemaKey As String, token As BToken, ms As Stream)

            If token Is Nothing Then
                ms.WriteByte(SOBJ_NULL_TAG)
                Return
            End If

            If token.IsNull Then

                Dim tc As JsonFieldType = GetFieldTypeCode(token)
                Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(tc)

                If tc = JsonFieldType.Unknown Then
                    ms.WriteByte(SOBJ_NULL_TAG)
                    Return
                End If

                If baseTc = JsonFieldType.Object Then

                    If JsonField.FieldTypeIsArray(tc) Then
                        ms.WriteByte(CByte(tc))
                        WriteArray(JsonFieldType.Object, expectedRefSchemaKey, Nothing, ms)
                    Else
                        ms.WriteByte(SOBJ_NULL_TAG)
                    End If

                    Return
                End If

                ms.WriteByte(CByte(tc))

                If JsonField.FieldTypeIsArray(tc) Then
                    WriteArray(baseTc, Nothing, Nothing, ms)
                Else
                    WriteScalar(baseTc, Nothing, token, ms)
                End If

                Return
            End If

            If TypeOf token Is BObject Then

                Dim obj As BObject = DirectCast(token, BObject)
                Dim runtimeSs As SessionSchema = EnsureObjectSchema(obj)
                Dim expectedKey As String = If(expectedRefSchemaKey, EnsureGenericObjectSchema().JS.Key)

                If String.Equals(runtimeSs.JS.Key, expectedKey, StringComparison.Ordinal) Then
                    ms.WriteByte(SOBJ_PRESENT_EXPECTED_SCHEMA)
                Else
                    ms.Write(runtimeSs.Pointer.len)
                    ms.Write(runtimeSs.Pointer.data)
                End If

                EnterRef(obj)
                Try
                    If obj.ObjectKind = BObject.Enum_ObjectKind.Map Then
                        WriteMapBody(obj, runtimeSs.JS.DictValueType, runtimeSs.JS.DictValueSchemaKey, ms)
                    Else
                        WritePlainObjectBody(obj, runtimeSs.JS.Fields, ms)
                    End If
                Finally
                    ExitRef(obj)
                End Try

                Return
            End If

            If TypeOf token Is BArray Then

                Dim arr As BArray = DirectCast(token, BArray)
                Dim tc As JsonFieldType = InferArrayTypeCode(arr)
                Dim baseTc As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(tc)

                ms.WriteByte(CByte(tc))

                EnterRef(arr)
                Try
                    If baseTc = JsonFieldType.Object Then
                        Dim arrayExpectedKey As String = If(expectedRefSchemaKey, EnsureGenericObjectSchema().JS.Key)
                        WriteArray(JsonFieldType.Object, arrayExpectedKey, arr, ms)
                    Else
                        WriteArray(baseTc, Nothing, arr, ms)
                    End If
                Finally
                    ExitRef(arr)
                End Try

                Return
            End If

            Dim primitiveTc As JsonFieldType = GetFieldTypeCode(token)
            ms.WriteByte(CByte(primitiveTc))
            WriteScalar(JsonField.FieldTypeWithoutArrayFlag(primitiveTc), Nothing, token, ms)

        End Sub

#End Region

#Region "Files Zone (optional per-session file area)"

        Private ReadOnly _filesList As New List(Of KeyValuePair(Of String, Byte()))()
        Private ReadOnly _fileIndexByName As New Dictionary(Of String, Integer)(StringComparer.Ordinal)

        Public Sub New(Optional files As IDictionary(Of String, Byte()) = Nothing)
            SetFiles(files)
        End Sub

        Public Sub SetFiles(files As IDictionary(Of String, Byte()))
            _filesList.Clear()
            _fileIndexByName.Clear()

            If files Is Nothing Then Return

            For Each kv As KeyValuePair(Of String, Byte()) In files
                AddFile(kv.Key, kv.Value)
            Next
        End Sub

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

        Friend ReadOnly Property HasFiles As Boolean
            Get
                Return _filesList.Count <> 0
            End Get
        End Property

        Public Sub WriteFilesZone(ms As Stream)

            If ms Is Nothing Then Throw New ArgumentNullException(NameOf(ms))
            If _filesList.Count = 0 Then Return

            Dim count As Integer = _filesList.Count
            Dim filesBodyLength As Long = Writer.GetLUINTEncodedByteCount(CLng(count))

            ' Pre-pass:
            '   Compute the exact FILES body length without allocating an intermediate stream.
            '   filesBodyLength covers:
            '   [fileCount][file0][file1]...[fileN]
            For i As Integer = 0 To count - 1

                Dim e = _filesList(i)

                filesBodyLength += Writer.GetLSTREncodedByteCount(e.Key)
                filesBodyLength += Writer.GetBarrEncodedByteCount(e.Value)

            Next

            ' Zone prefix:
            '   [filesByteLength][fileCount]
            Writer.WriteLUINT(filesBodyLength, ms)
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
            _initialized.Clear()
            _genericObjectSchema = Nothing

            _filesList.Clear()
            _fileIndexByName.Clear()
        End Sub

#End Region

    End Class

End Namespace