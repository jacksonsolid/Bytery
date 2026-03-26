Imports System.Globalization
Imports System.Reflection
Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Object-like token in the LINQ DOM layer.
    '''
    ''' This type stores named child tokens using ordinal key comparison and behaves
    ''' similarly to a JSON object:
    ''' - keys map to <see cref="BToken"/> values
    ''' - missing keys can be queried directly or through path-based access
    ''' - values can be materialized back to CLR dictionaries / objects
    '''
    ''' Path-aware behavior:
    ''' - direct key lookup is attempted first
    ''' - if the requested name looks like a path (<c>.</c>, <c>[...]</c>, <c>?</c>, <c>*</c>),
    '''   lookup is delegated to the generic path resolver in <see cref="BToken"/>
    ''' </summary>
    Public NotInheritable Class BObject
        Inherits BToken
        Implements IEnumerable(Of KeyValuePair(Of String, BToken))

        Public Enum Enum_ObjectKind
            PlainObject
            [Map]
        End Enum

        Private ReadOnly _values As Dictionary(Of String, BToken)
        Private ReadOnly _objectKind As Enum_ObjectKind
        Private ReadOnly _mapValueFieldCode As JsonFieldType

        Public Sub New()
            Me.New(Enum_ObjectKind.PlainObject, JsonFieldType.Unknown)
        End Sub

        Public Sub New(objectKind As Enum_ObjectKind, Optional mapValueFieldCode As JsonFieldType = JsonFieldType.Unknown)
            _values = New Dictionary(Of String, BToken)(StringComparer.Ordinal)
            _objectKind = objectKind

            If objectKind = Enum_ObjectKind.Map Then
                _mapValueFieldCode = mapValueFieldCode
            Else
                _mapValueFieldCode = JsonFieldType.Unknown
            End If
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                Return JsonFieldType.Object
            End Get
        End Property

        Public ReadOnly Property ObjectKind As Enum_ObjectKind
            Get
                Return _objectKind
            End Get
        End Property

        Public ReadOnly Property MapValueFieldCode As JsonFieldType
            Get
                Return _mapValueFieldCode
            End Get
        End Property

        Private Function NormalizeObjectValue(value As BToken) As BToken

            If value Is Nothing Then
                If _objectKind = Enum_ObjectKind.Map Then
                    Return BNull.Instance(_mapValueFieldCode)
                End If

                Return BNull.Instance(JsonFieldType.Unknown)
            End If

            If value.IsNull Then

                If _objectKind = Enum_ObjectKind.Map Then

                    If _mapValueFieldCode <> JsonFieldType.Unknown AndAlso
                       value.FieldCode <> JsonFieldType.Unknown AndAlso
                       value.FieldCode <> _mapValueFieldCode Then

                        Throw New InvalidOperationException(
                            $"Map expects values of type {_mapValueFieldCode}, but received {value.FieldCode}.")

                    End If

                    If _mapValueFieldCode <> JsonFieldType.Unknown Then
                        Return BNull.Instance(_mapValueFieldCode)
                    End If

                End If

                Return value
            End If

            If _objectKind = Enum_ObjectKind.Map AndAlso _mapValueFieldCode <> JsonFieldType.Unknown Then

                If _mapValueFieldCode <> JsonFieldType.Object AndAlso value.FieldCode <> _mapValueFieldCode Then
                    Throw New InvalidOperationException($"Map expects values of type {_mapValueFieldCode}, but received {value.FieldCode}.")
                End If

            End If

            Return value

        End Function

        Private Shared Function InferMapValueFieldCode(tokens As IEnumerable(Of BToken)) As JsonFieldType

            Dim firstCode As JsonFieldType = JsonFieldType.Unknown

            For Each token As BToken In tokens
                If token Is Nothing Then Continue For
                If token.FieldCode = JsonFieldType.Unknown Then Continue For
                If firstCode <> JsonFieldType.Unknown AndAlso firstCode <> token.FieldCode Then Return JsonFieldType.Object
                firstCode = token.FieldCode
            Next

            Return firstCode

        End Function

        ''' <summary>
        ''' Gets the base token kind for this node.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Object
            End Get
        End Property

        ''' <summary>
        ''' Gets whether this object token is an array.
        ''' Always returns <c>False</c> for <see cref="BObject"/>.
        ''' </summary>
        Public Overrides ReadOnly Property IsArray As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Gets whether this token is a primitive scalar.
        ''' Always returns <c>False</c> for <see cref="BObject"/>.
        ''' </summary>
        Public Overrides ReadOnly Property IsPrimitive As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Gets the number of members currently stored in the object.
        ''' </summary>
        Public Overrides ReadOnly Property Count As Integer
            Get
                Return _values.Count
            End Get
        End Property

        ''' <summary>
        ''' Gets the collection of keys currently present in the object.
        ''' </summary>
        Public ReadOnly Property Keys As ICollection(Of String)
            Get
                Return _values.Keys
            End Get
        End Property

        ''' <summary>
        ''' Gets the collection of values currently present in the object.
        ''' </summary>
        Public ReadOnly Property Values As ICollection(Of BToken)
            Get
                Return _values.Values
            End Get
        End Property

        ''' <summary>
        ''' Gets or sets a member by name.
        '''
        ''' Read behavior:
        ''' 1. Try direct key lookup first.
        ''' 2. If the supplied text looks like a path, resolve it as a path.
        ''' 3. Otherwise fail as a required direct-key lookup.
        '''
        ''' Write behavior:
        ''' - <c>Nothing</c> values are normalized to <see cref="BNull.Instance"/>.
        ''' </summary>
        Default Public Overrides Property Item(name As String) As BToken
            Get
                If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))

                Dim token As BToken = Nothing

                If _values.TryGetValue(name, token) Then
                    Return token
                End If

                Dim isPath As Boolean =
                    name.StartsWith("*", StringComparison.Ordinal) OrElse
                    name.IndexOf("?"c) >= 0 OrElse
                    name.IndexOf("."c) >= 0 OrElse
                    name.IndexOf("["c) >= 0

                If isPath Then
                    Return MyBase.GetValue(name)
                End If

                Throw New Exception("Key not present on the object: " & name)
            End Get

            Set(value As BToken)
                If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))
                _values(name) = NormalizeObjectValue(value)
            End Set
        End Property

        ''' <summary>
        ''' Adds a new member to the object.
        '''
        ''' This mirrors <see cref="Dictionary(Of TKey, TValue).Add"/> semantics:
        ''' an exception is thrown if the key already exists.
        ''' </summary>
        Public Sub Add(name As String, value As BToken)
            If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))
            _values.Add(name, NormalizeObjectValue(value))
        End Sub

        ''' <summary>
        ''' Returns whether the object contains the specified direct key.
        ''' </summary>
        Public Function ContainsKey(name As String) As Boolean
            If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))
            Return _values.ContainsKey(name)
        End Function

        ''' <summary>
        ''' Removes a direct key from the object.
        ''' </summary>
        Public Function Remove(name As String) As Boolean
            If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))
            Return _values.Remove(name)
        End Function

        ''' <summary>
        ''' Removes all members from the object.
        ''' </summary>
        Public Sub Clear()
            _values.Clear()
        End Sub

        ''' <summary>
        ''' Tries to retrieve a direct child token without invoking path semantics.
        ''' </summary>
        Public Function TryGetDirectValue(name As String, ByRef value As BToken) As Boolean
            If name Is Nothing Then Throw New ArgumentNullException(NameOf(name))
            Return _values.TryGetValue(name, value)
        End Function

        ''' <summary>
        ''' Converts the object token to a requested CLR target.
        '''
        ''' Supported targets:
        ''' - <see cref="Object"/>
        ''' - <c>Dictionary(Of String, Object)</c>
        ''' - <c>Dictionary(Of String, BToken)</c>
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T

            Dim targetType As Type = GetType(T)

            If targetType Is GetType(Object) Then
                Return CType(ToObject(), T)
            End If

            If targetType Is GetType(Dictionary(Of String, Object)) Then
                Return CType(CType(ToObject(), Object), T)
            End If

            If targetType Is GetType(Dictionary(Of String, BToken)) Then
                Return CType(CType(_values, Object), T)
            End If

            Throw New InvalidCastException(
            $"Cannot convert {NameOf(BObject)} to {targetType.FullName}. " &
            $"Supported targets are Object, Dictionary(Of String, Object), and Dictionary(Of String, BToken).")

        End Function

        ''' <summary>
        ''' Resolves a dotted / indexed path starting from the current object.
        '''
        ''' Supported local segment forms include:
        ''' - <c>name</c>
        ''' - <c>name?</c>
        ''' - <c>name[index]</c>
        ''' - <c>name[index]?</c>
        '''
        ''' Resolution rules:
        ''' - object-key optionality can be declared with <c>?</c>
        ''' - a leading <c>*</c> on the whole path also marks the path as optional
        ''' - if a segment has an index tail, the tail is delegated to the resolved child token
        ''' </summary>
        Friend Overrides Function GetValue(Of T)(paths As String(), pathIndex As Integer, pathOptional As Boolean, [default] As T) As T

            If paths Is Nothing OrElse paths.Length = 0 Then
                Throw New ArgumentException("paths cannot be null or empty.", NameOf(paths))
            End If

            If pathIndex < 0 OrElse pathIndex >= paths.Length Then
                Throw New ArgumentOutOfRangeException(NameOf(pathIndex))
            End If

            Dim rawPath As String = paths(pathIndex)
            If String.IsNullOrWhiteSpace(rawPath) Then
                Throw New Exception("Invalid path: " & String.Join(".", paths))
            End If

            Dim bracketPos As Integer = rawPath.IndexOf("["c)

            Dim keyPart As String
            Dim tail As String

            If bracketPos >= 0 Then
                keyPart = rawPath.Substring(0, bracketPos)
                tail = rawPath.Substring(bracketPos)
            Else
                keyPart = rawPath
                tail = ""
            End If

            Dim keyOptional As Boolean = False

            If keyPart.EndsWith("?"c) Then
                keyOptional = True
                keyPart = keyPart.Substring(0, keyPart.Length - 1)
            End If

            If String.IsNullOrWhiteSpace(keyPart) Then
                Throw New Exception("Invalid object key in path: " & String.Join(".", paths))
            End If

            If Not Me.ContainsKey(keyPart) Then
                If pathOptional OrElse keyOptional Then Return [default]
                Throw New Exception("Key not present on the object: " & String.Join(".", paths))
            End If

            Dim token As BToken = Me.Item(keyPart)

            If token Is Nothing OrElse token.IsNull Then
                If pathOptional OrElse keyOptional Then Return [default]
                Throw New Exception("Key has null value on the object: " & String.Join(".", paths))
            End If

            If tail.Length > 0 Then

                If pathIndex >= paths.Length - 1 Then
                    Return token.GetValue(Of T)(New String() {tail}, 0, pathOptional, [default])
                Else
                    Dim nextPaths(paths.Length - pathIndex - 1) As String
                    nextPaths(0) = tail

                    Dim j As Integer = 1
                    For i As Integer = pathIndex + 1 To paths.Length - 1
                        nextPaths(j) = paths(i)
                        j += 1
                    Next

                    Return token.GetValue(Of T)(nextPaths, 0, pathOptional, [default])
                End If

            End If

            If pathIndex >= paths.Length - 1 Then

                If GetType(BToken).IsAssignableFrom(GetType(T)) Then
                    Return CType(CType(token, Object), T)
                End If

                Return token.GetValue(Of T)()

            End If

            Return token.GetValue(Of T)(paths, pathIndex + 1, pathOptional, [default])

        End Function

        ''' <summary>
        ''' Converts the object token to a CLR dictionary tree.
        '''
        ''' Each child token is converted recursively through <see cref="BToken.ToObject"/>.
        ''' </summary>
        Public Overrides Function ToObject() As Object

            Dim result As New Dictionary(Of String, Object)(_values.Count, StringComparer.Ordinal)

            For Each kv As KeyValuePair(Of String, BToken) In _values
                If kv.Value Is Nothing Then
                    result(kv.Key) = Nothing
                Else
                    result(kv.Key) = kv.Value.ToObject()
                End If
            Next

            Return result

        End Function

#Region "ToString / ToJson"

        ''' <summary>
        ''' Returns the compact JSON representation of the object.
        ''' </summary>
        Public Overrides Function ToString() As String
            Return ToString(False)
        End Function

        ''' <summary>
        ''' Returns the JSON representation of the object with optional indentation.
        ''' </summary>
        Public Overrides Function ToString(indent As Boolean) As String
            Return ToJsonString(indent, 0)
        End Function

        ''' <summary>
        ''' Serializes the object token to JSON text.
        '''
        ''' Formatting rules:
        ''' - compact mode emits no extra whitespace
        ''' - indented mode uses 2 spaces per nesting level
        ''' - missing child token references are normalized as <see cref="BNull.Instance"/>
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String

            Dim sb As New System.Text.StringBuilder()
            sb.Append("{"c)

            Dim first As Boolean = True

            For Each kv As KeyValuePair(Of String, BToken) In _values

                If first Then
                    first = False
                Else
                    sb.Append(","c)
                End If

                If indent Then
                    sb.AppendLine()
                    sb.Append(New String(" "c, (indentCount + 1) * 2))
                End If

                sb.Append(EscapeJsonString(kv.Key))

                If indent Then
                    sb.Append(": ")
                Else
                    sb.Append(":"c)
                End If

                Dim nullCode As JsonFieldType = If(_objectKind = Enum_ObjectKind.Map, _mapValueFieldCode, JsonFieldType.Unknown)
                Dim token As BToken = If(kv.Value, BNull.Instance(nullCode))
                sb.Append(token.ToJsonString(indent, indentCount + 1))

            Next

            If indent AndAlso _values.Count > 0 Then
                sb.AppendLine()
                sb.Append(New String(" "c, indentCount * 2))
            End If

            sb.Append("}"c)

            Return sb.ToString()

        End Function

#End Region

        ''' <summary>
        ''' Returns a generic enumerator over the stored key/value pairs.
        ''' </summary>
        Public Function GetEnumerator() As IEnumerator(Of KeyValuePair(Of String, BToken)) _
        Implements IEnumerable(Of KeyValuePair(Of String, BToken)).GetEnumerator

            Return _values.GetEnumerator()
        End Function

        ''' <summary>
        ''' Returns a non-generic enumerator over the stored key/value pairs.
        ''' </summary>
        Private Function GetEnumeratorNonGeneric() As IEnumerator _
        Implements IEnumerable.GetEnumerator

            Return _values.GetEnumerator()
        End Function

#Region "FromObject"

        Friend Shared Function FromAnyDictionary(value As Object, Optional declaredType As Type = Nothing) As BObject

            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))

            Dim dict As IDictionary = TryCast(value, IDictionary)
            If dict IsNot Nothing Then
                Return FromDictionary(dict, declaredType)
            End If

            Dim en As IEnumerable = TryCast(value, IEnumerable)
            If en Is Nothing Then
                Throw New ArgumentException($"Value of type {value.GetType().FullName} is not enumerable.", NameOf(value))
            End If

            Dim declaredValueType As Type = GetDeclaredDictionaryValueType(declaredType)
            Dim declaredMapCode As JsonFieldType = DotNet.Cache.TypeToFieldType(declaredValueType)

            Dim entries As New List(Of KeyValuePair(Of String, BToken))()

            For Each entry As Object In en

                If entry Is Nothing Then
                    Continue For
                End If

                Dim entryType As Type = entry.GetType()

                Dim keyProp As PropertyInfo = entryType.GetProperty("Key", BindingFlags.Instance Or BindingFlags.Public)
                Dim valueProp As PropertyInfo = entryType.GetProperty("Value", BindingFlags.Instance Or BindingFlags.Public)

                If keyProp Is Nothing OrElse valueProp Is Nothing Then
                    Throw New InvalidOperationException($"Dictionary-like entry type {entryType.FullName} does not expose public Key/Value properties.")
                End If

                Dim keyObj As Object = keyProp.GetValue(entry, Nothing)
                Dim valObj As Object = valueProp.GetValue(entry, Nothing)

                Dim key As String = If(keyObj Is Nothing, "null", Convert.ToString(keyObj, CultureInfo.InvariantCulture))
                Dim effectiveValueType As Type = If(declaredValueType, valueProp.PropertyType)
                Dim token As BToken = BToken.FromObject(valObj, effectiveValueType)

                entries.Add(New KeyValuePair(Of String, BToken)(key, token))

            Next

            Dim mapCode As JsonFieldType =
        If(declaredMapCode <> JsonFieldType.Unknown,
           declaredMapCode,
           InferMapValueFieldCode(entries.Select(Function(x) x.Value)))

            Dim result As New BObject(Enum_ObjectKind.Map, mapCode)

            For Each entry In entries
                result.Add(entry.Key, entry.Value)
            Next

            Return result

        End Function

        Private Shared Function GetDeclaredDictionaryValueType(declaredType As Type) As Type

            If declaredType Is Nothing Then
                Return Nothing
            End If

            If declaredType.IsGenericType Then

                Dim gtd As Type = declaredType.GetGenericTypeDefinition()

                If gtd Is GetType(IDictionary(Of ,)) OrElse
                   gtd Is GetType(Dictionary(Of ,)) OrElse
                   gtd Is GetType(IReadOnlyDictionary(Of ,)) Then

                    Return declaredType.GetGenericArguments()(1)
                End If

            End If

            For Each itf As Type In declaredType.GetInterfaces()

                If itf.IsGenericType Then

                    Dim gtd As Type = itf.GetGenericTypeDefinition()

                    If gtd Is GetType(IDictionary(Of ,)) OrElse gtd Is GetType(IReadOnlyDictionary(Of ,)) Then
                        Return itf.GetGenericArguments()(1)
                    End If

                End If

            Next

            Return Nothing

        End Function

        ''' <summary>
        ''' Converts a CLR value to a <see cref="BObject"/>.
        '''
        ''' The supplied value must materialize to an object-like token.
        ''' Primitive values and arrays are rejected.
        ''' </summary>
        Public Shared Shadows Function FromObject(value As Object) As BObject

            If value Is Nothing Then
                Throw New ArgumentNullException(NameOf(value))
            End If

            Dim token As BToken = BToken.FromObject(value)
            Dim obj As BObject = TryCast(token, BObject)

            If obj Is Nothing Then
                Throw New ArgumentException($"The supplied value of type {value.GetType().FullName} does not materialize to a {NameOf(BObject)}.")
            End If

            Return obj

        End Function

        ''' <summary>
        ''' Builds a <see cref="BObject"/> from a non-generic <see cref="IDictionary"/>.
        '''
        ''' Dictionary keys are converted to strings using invariant culture.
        ''' A null key is materialized as the literal string <c>"null"</c>.
        ''' </summary>
        Friend Shared Function FromDictionary(dict As IDictionary, Optional declaredType As Type = Nothing) As BObject

            If dict Is Nothing Then Throw New ArgumentNullException(NameOf(dict))

            Dim declaredValueType As Type = Nothing
            Dim declaredMapCode As JsonFieldType = JsonFieldType.Unknown

            If declaredType IsNot Nothing Then
                Dim info = DotNet.Cache.GetInfo(declaredType)

                If info IsNot Nothing AndAlso info.IsDictionary AndAlso info.DictValue IsNot Nothing Then
                    declaredValueType = info.DictValue
                    declaredMapCode = DotNet.Cache.TypeToFieldType(declaredValueType)
                End If
            End If

            Dim entries As New List(Of KeyValuePair(Of String, BToken))()

            For Each entry As DictionaryEntry In dict
                Dim key As String =
            If(entry.Key Is Nothing, "null", Convert.ToString(entry.Key, CultureInfo.InvariantCulture))

                Dim token As BToken = BToken.FromObject(entry.Value, declaredValueType)

                entries.Add(New KeyValuePair(Of String, BToken)(key, token))
            Next

            Dim mapCode As JsonFieldType = If(declaredMapCode <> JsonFieldType.Unknown, declaredMapCode, InferMapValueFieldCode(entries.Select(Function(x) x.Value)))

            Dim result As New BObject(Enum_ObjectKind.Map, mapCode)

            For Each entry In entries
                result.Add(entry.Key, entry.Value)
            Next

            Return result

        End Function

        Friend Shared Function FromPlainObject(value As Object, Optional declaredType As Type = Nothing) As BObject

            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))

            Dim result As New BObject()
            Dim runtimeType As Type = value.GetType()
            Dim t As Type = GetDeclaredPlainObjectType(runtimeType, declaredType)

            Dim names As New HashSet(Of String)(StringComparer.Ordinal)

            Dim props As PropertyInfo() =
        t.GetProperties(BindingFlags.Instance Or BindingFlags.Public)

            Array.Sort(props, Function(a, b) StringComparer.Ordinal.Compare(a.Name, b.Name))

            For Each p As PropertyInfo In props

                If Not p.CanRead Then Continue For
                If p.GetIndexParameters().Length <> 0 Then Continue For

                If Not names.Add(p.Name) Then
                    Throw New InvalidOperationException($"Duplicate public member name '{p.Name}' in type {t.FullName}.")
                End If

                Dim memberValue As Object = p.GetValue(value, Nothing)
                Dim token As BToken = BToken.FromObject(memberValue, p.PropertyType)

                If token IsNot Nothing Then
                    result.Add(p.Name, token)
                End If

            Next

            Dim fields As FieldInfo() = t.GetFields(BindingFlags.Instance Or BindingFlags.Public)

            Array.Sort(fields, Function(a, b) StringComparer.Ordinal.Compare(a.Name, b.Name))

            For Each f As FieldInfo In fields

                If f.IsStatic Then Continue For

                If Not names.Add(f.Name) Then
                    Throw New InvalidOperationException($"Duplicate public member name '{f.Name}' in type {t.FullName}.")
                End If

                Dim memberValue As Object = f.GetValue(value)
                Dim token As BToken = BToken.FromObject(memberValue, f.FieldType)

                If token IsNot Nothing Then
                    result.Add(f.Name, token)
                End If

            Next

            Return result

        End Function

        Private Shared Function GetDeclaredPlainObjectType(runtimeType As Type, declaredType As Type) As Type

            If runtimeType Is Nothing Then Return Nothing
            If declaredType Is Nothing Then Return runtimeType
            If declaredType Is GetType(Object) Then Return runtimeType
            If declaredType Is GetType(ValueType) Then Return runtimeType
            If declaredType.IsPrimitive Then Return runtimeType
            If declaredType.IsEnum Then Return runtimeType
            If declaredType Is GetType(String) Then Return runtimeType
            If declaredType Is GetType(Byte()) Then Return runtimeType

            If declaredType.IsAssignableFrom(runtimeType) Then
                Return declaredType
            End If

            Return runtimeType

        End Function

#End Region

        ''' <summary>
        ''' Parses JSON text and requires the root token to be an object.
        ''' </summary>
        Public Shared Shadows Function Parse(json As String) As BObject

            Dim token As BToken = BToken.ParseJSON(json)
            Dim obj As BObject = TryCast(token, BObject)

            If obj Is Nothing Then
                Throw New Exception("JSON root is not an object.")
            End If

            Return obj

        End Function

    End Class

End Namespace