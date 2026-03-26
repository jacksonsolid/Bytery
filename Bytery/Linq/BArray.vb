Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Array-like token in the LINQ DOM layer.
    '''
    ''' This type stores an ordered list of <see cref="BToken"/> items and behaves
    ''' similarly to a JSON array:
    ''' - elements are indexed numerically
    ''' - values are normalized so <c>Nothing</c> becomes <see cref="BNull.Instance"/>
    ''' - the array can be materialized back to CLR arrays / lists
    '''
    ''' Path behavior:
    ''' - path segments must start with an index expression such as <c>[0]</c>
    ''' - optional access is supported with <c>?</c>, for example <c>[3]?</c>
    ''' - chained array navigation such as <c>[0][1]</c> is supported
    ''' </summary>
    Public NotInheritable Class BArray
        Inherits BToken
        Implements IEnumerable(Of BToken)

        ''' <summary>
        ''' Internal ordered storage for array elements.
        ''' </summary>
        Private ReadOnly _items As List(Of BToken)
        Private ReadOnly _elementFieldCode As JsonFieldType

        ''' <summary>
        ''' Creates an empty array token.
        ''' </summary>
        Public Sub New(fieldType As JsonFieldType)
            _items = New List(Of BToken)()
            _elementFieldCode = JsonField.FieldTypeWithoutArrayFlag(fieldType)
        End Sub

        Public Overrides ReadOnly Property FieldCode As JsonFieldType
            Get
                If _elementFieldCode = JsonFieldType.Unknown Then Return JsonFieldType.Unknown
                Return _elementFieldCode Or JsonFieldType.ArrayFlag
            End Get
        End Property

        ''' <summary>
        ''' Gets the base token kind for this node.
        '''
        ''' This currently returns <see cref="Enum_BaseType.Object"/>, which matches
        ''' the existing DOM classification used by this project.
        ''' </summary>
        Public Overrides ReadOnly Property BaseType As Enum_BaseType
            Get
                Return Enum_BaseType.Object
            End Get
        End Property

        ''' <summary>
        ''' Gets whether this token is an array.
        ''' Always returns <c>True</c> for <see cref="BArray"/>.
        ''' </summary>
        Public Overrides ReadOnly Property IsArray As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Gets whether this token is a primitive scalar.
        ''' Always returns <c>False</c> for <see cref="BArray"/>.
        ''' </summary>
        Public Overrides ReadOnly Property IsPrimitive As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Gets the number of elements currently stored in the array.
        ''' </summary>
        Public Overrides ReadOnly Property Count As Integer
            Get
                Return _items.Count
            End Get
        End Property

        ''' <summary>
        ''' Gets or sets an element by zero-based index.
        '''
        ''' Assigned values are normalized so <c>Nothing</c> becomes <see cref="BNull.Instance"/>.
        ''' </summary>
        Default Public Overrides Property Item(index As Integer) As BToken
            Get
                Return _items(index)
            End Get
            Set(value As BToken)
                _items(index) = NormalizeArrayItem(value)
            End Set
        End Property

        ''' <summary>
        ''' Appends an element to the end of the array.
        '''
        ''' The value is normalized so <c>Nothing</c> becomes <see cref="BNull.Instance"/>.
        ''' </summary>
        Public Sub Add(value As BToken)
            _items.Add(NormalizeArrayItem(value))
        End Sub

        ''' <summary>
        ''' Inserts an element at the specified zero-based index.
        '''
        ''' The value is normalized so <c>Nothing</c> becomes <see cref="BNull.Instance"/>.
        ''' </summary>
        Public Sub Insert(index As Integer, value As BToken)
            _items.Insert(index, NormalizeArrayItem(value))
        End Sub

        ''' <summary>
        ''' Removes the first occurrence of the specified token value.
        '''
        ''' A <c>Nothing</c> argument is treated as <see cref="BNull.Instance"/>.
        ''' </summary>
        Public Function Remove(value As BToken) As Boolean
            Return _items.Remove(If(value, BNull.Instance(_elementFieldCode)))
        End Function

        ''' <summary>
        ''' Removes the element at the specified zero-based index.
        ''' </summary>
        Public Sub RemoveAt(index As Integer)
            _items.RemoveAt(index)
        End Sub

        ''' <summary>
        ''' Removes all elements from the array.
        ''' </summary>
        Public Sub Clear()
            _items.Clear()
        End Sub

        ''' <summary>
        ''' Converts the array token to a requested CLR target.
        '''
        ''' Supported targets:
        ''' - <see cref="Object"/>
        ''' - <c>Object()</c>
        ''' - <c>BToken()</c>
        ''' - <c>List(Of Object)</c>
        ''' - <c>List(Of BToken)</c>
        ''' </summary>
        Public Overrides Function GetValue(Of T)() As T

            Dim targetType As Type = GetType(T)

            If targetType Is GetType(Object) Then
                Return CType(ToObject(), T)
            End If

            If targetType Is GetType(Object()) Then
                Return CType(CType(ToObject(), Object), T)
            End If

            If targetType Is GetType(BToken()) Then
                Return CType(CType(_items.ToArray(), Object), T)
            End If

            If targetType Is GetType(List(Of Object)) Then
                Dim objs As Object() = DirectCast(ToObject(), Object())
                Return CType(CType(New List(Of Object)(objs), Object), T)
            End If

            If targetType Is GetType(List(Of BToken)) Then
                Return CType(CType(New List(Of BToken)(_items), Object), T)
            End If

            Throw New InvalidCastException(
                $"Cannot convert {NameOf(BArray)} to {targetType.FullName}. " &
                $"Supported targets are Object, Object(), BToken(), List(Of Object), and List(Of BToken).")

        End Function

        ''' <summary>
        ''' Resolves a path segment starting from the current array.
        '''
        ''' Supported local segment forms:
        ''' - <c>[0]</c>
        ''' - <c>[0]?</c>
        ''' - <c>[0][1]</c>
        ''' - <c>[0][1]?</c>
        '''
        ''' Resolution rules:
        ''' - the segment must begin with an array index expression
        ''' - optional array access is declared with <c>?</c> immediately after <c>]</c>
        ''' - any remaining tail is delegated to the resolved child token
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

            If rawPath(0) <> "["c Then
                Throw New Exception("Array path must start with '[': " & rawPath)
            End If

            Dim closePos As Integer = rawPath.IndexOf("]"c)
            If closePos <= 1 Then
                Throw New Exception("Invalid array index path: " & rawPath)
            End If

            Dim indexText As String = rawPath.Substring(1, closePos - 1)
            Dim idx As Integer

            If Not Integer.TryParse(indexText, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, idx) Then
                Throw New Exception("Invalid array index path: " & rawPath)
            End If

            Dim indexOptional As Boolean = False
            Dim tailStart As Integer = closePos + 1

            If tailStart < rawPath.Length AndAlso rawPath(tailStart) = "?"c Then
                indexOptional = True
                tailStart += 1
            End If

            Dim tail As String = rawPath.Substring(tailStart)

            If tail.Length > 0 AndAlso tail(0) <> "["c Then
                Throw New Exception("Invalid array path continuation: " & rawPath)
            End If

            If idx < 0 OrElse idx >= Me.Count Then
                If pathOptional OrElse indexOptional Then Return [default]
                Throw New Exception("Array index out of range: " & String.Join(".", paths))
            End If

            Dim token As BToken = Me.Item(idx)

            If token Is Nothing OrElse token.IsNull Then
                If pathOptional OrElse indexOptional Then Return [default]
                Throw New Exception("Null value on array path: " & String.Join(".", paths))
            End If

            If tail.Length > 0 Then

                Dim nextPaths(paths.Length - pathIndex - 1) As String
                nextPaths(0) = tail

                Dim j As Integer = 1
                For i As Integer = pathIndex + 1 To paths.Length - 1
                    nextPaths(j) = paths(i)
                    j += 1
                Next

                Return token.GetValue(Of T)(nextPaths, 0, pathOptional, [default])

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
        ''' Converts the array token to a CLR object array.
        '''
        ''' Each child token is converted recursively through <see cref="BToken.ToObject"/>.
        ''' </summary>
        Public Overrides Function ToObject() As Object

            If _items.Count = 0 Then
                Return Array.Empty(Of Object)()
            End If

            Dim result(_items.Count - 1) As Object

            For i As Integer = 0 To _items.Count - 1
                result(i) = _items(i).ToObject()
            Next

            Return result

        End Function

#Region "ToString / ToJson"

        ''' <summary>
        ''' Returns the compact JSON representation of the array.
        ''' </summary>
        Public Overrides Function ToString() As String
            Return Me.ToString(False)
        End Function

        ''' <summary>
        ''' Returns the JSON representation of the array with optional indentation.
        ''' </summary>
        Public Overrides Function ToString(indent As Boolean) As String
            Return Me.ToJsonString(indent, 0)
        End Function

        ''' <summary>
        ''' Serializes the array token to JSON text.
        '''
        ''' Formatting rules:
        ''' - compact mode emits no extra whitespace
        ''' - indented mode uses 2 spaces per nesting level
        ''' - missing child token references are normalized as <see cref="BNull.Instance"/>
        ''' </summary>
        Friend Overrides Function ToJsonString(indent As Boolean, indentCount As Integer) As String

            Dim sb As New System.Text.StringBuilder()
            sb.Append("["c)

            For i As Integer = 0 To _items.Count - 1

                If i > 0 Then
                    sb.Append(","c)
                End If

                If indent Then
                    sb.AppendLine()
                    sb.Append(New String(" "c, (indentCount + 1) * 2))
                End If

                Dim token As BToken = If(_items(i), BNull.Instance(_elementFieldCode))
                sb.Append(token.ToJsonString(indent, indentCount + 1))

            Next

            If indent AndAlso _items.Count > 0 Then
                sb.AppendLine()
                sb.Append(New String(" "c, indentCount * 2))
            End If

            sb.Append("]"c)

            Return sb.ToString()

        End Function

#End Region

        Private Function NormalizeArrayItem(value As BToken) As BToken

            If value Is Nothing OrElse value.IsNull Then
                Return BNull.Instance(_elementFieldCode)
            End If

            If _elementFieldCode = JsonFieldType.Unknown Then
                Return value
            End If

            ' Object[] pode conter override para primitivo no slot SOBJ.
            If _elementFieldCode = JsonFieldType.Object Then
                Return value
            End If

            If value.FieldCode <> _elementFieldCode Then
                Throw New InvalidOperationException($"Array expects items of type {_elementFieldCode}, but received {value.FieldCode}.")
            End If

            Return value

        End Function

        ''' <summary>
        ''' Returns a generic enumerator over the stored array items.
        ''' </summary>
        Public Function GetEnumerator() As IEnumerator(Of BToken) _
            Implements IEnumerable(Of BToken).GetEnumerator

            Return _items.GetEnumerator()
        End Function

        ''' <summary>
        ''' Returns a non-generic enumerator over the stored array items.
        ''' </summary>
        Private Function GetEnumeratorNonGeneric() As IEnumerator _
            Implements IEnumerable.GetEnumerator

            Return _items.GetEnumerator()
        End Function

        ''' <summary>
        ''' Builds a <see cref="BArray"/> from a CLR enumerable source.
        '''
        ''' Each source item is converted through <see cref="BToken.FromObject(Object)"/>.
        ''' When a declared enumerable type is provided, its element type is used to preserve
        ''' null/item typing.
        ''' </summary>
        Friend Shared Function FromEnumerable(source As IEnumerable, Optional declaredType As Type = Nothing) As BArray

            If source Is Nothing Then Throw New ArgumentNullException(NameOf(source))

            Dim declaredElementType As Type = GetDeclaredElementType(declaredType)
            Dim declaredElementCode As JsonFieldType = DotNet.Cache.TypeToFieldType(declaredElementType)

            Dim tokens As New List(Of BToken)()
            Dim code As JsonFieldType = declaredElementCode

            For Each item As Object In source

                Dim token As BToken = BToken.FromObject(item, declaredElementType)
                tokens.Add(token)

                If code = JsonFieldType.Unknown Then

                    If token IsNot Nothing AndAlso token.FieldCode <> JsonFieldType.Unknown Then
                        code = token.FieldCode
                    End If

                ElseIf code <> JsonFieldType.Object Then

                    If token IsNot Nothing AndAlso
               token.FieldCode <> JsonFieldType.Unknown AndAlso
               token.FieldCode <> code Then

                        code = JsonFieldType.Object
                    End If

                End If

            Next

            Dim result As New BArray(code)

            For Each token As BToken In tokens
                result.Add(token)
            Next

            Return result

        End Function

        Private Shared Function GetDeclaredElementType(declaredType As Type) As Type

            If declaredType Is Nothing Then Return Nothing
            If declaredType Is GetType(String) Then Return Nothing
            If declaredType Is GetType(Byte()) Then Return Nothing

            If declaredType.IsArray Then
                Return declaredType.GetElementType()
            End If

            If declaredType.IsGenericType Then

                Dim gtd As Type = declaredType.GetGenericTypeDefinition()

                If gtd Is GetType(IEnumerable(Of)) OrElse
                   gtd Is GetType(ICollection(Of)) OrElse
                   gtd Is GetType(IList(Of)) OrElse
                   gtd Is GetType(List(Of)) Then

                    Return declaredType.GetGenericArguments()(0)
                End If

            End If

            For Each itf As Type In declaredType.GetInterfaces()

                If itf.IsGenericType AndAlso itf.GetGenericTypeDefinition() Is GetType(IEnumerable(Of)) Then
                    Return itf.GetGenericArguments()(0)
                End If

            Next

            Return Nothing

        End Function

        ''' <summary>
        ''' Parses JSON text and requires the root token to be an array.
        ''' </summary>
        Public Shared Shadows Function Parse(json As String) As BArray

            Dim token As BToken = BToken.ParseJSON(json)
            Dim arr As BArray = TryCast(token, BArray)

            If arr Is Nothing Then
                Throw New Exception("JSON root is not an array.")
            End If

            Return arr

        End Function

    End Class

End Namespace