Imports System.Globalization
Imports System.Text
Imports Bytery.JSON

Namespace Linq

    ''' <summary>
    ''' Base abstraction for the Bytery JSON-like DOM layer.
    '''
    ''' This type represents any node that can exist in the in-memory tree:
    ''' - null
    ''' - number
    ''' - boolean
    ''' - date
    ''' - string
    ''' - bytes
    ''' - object / array
    '''
    ''' Main responsibilities:
    ''' - Expose a common runtime shape for all token types.
    ''' - Convert CLR values into token instances.
    ''' - Resolve dotted paths such as <c>"user.profile.name"</c>.
    ''' - Serialize tokens back to JSON text.
    ''' - Convert tokens back to CLR values through <c>GetValue</c> / <c>ToObject</c>.
    ''' </summary>
    Public MustInherit Class BToken

        ''' <summary>
        ''' High-level token kind used by the LINQ-style DOM.
        '''
        ''' Notes:
        ''' - <c>Object</c> covers both object and array containers.
        ''' - Array-ness is exposed separately by <see cref="IsArray"/>.
        ''' </summary>
        Public Enum Enum_BaseType
            [Null]
            Number
            [Boolean]
            [Date]
            [String]
            [Byte]
            [Object]
        End Enum

        ''' <summary>
        ''' Gets the base token kind for the current node.
        ''' </summary>
        Public MustOverride ReadOnly Property BaseType As Enum_BaseType
        Public MustOverride ReadOnly Property FieldCode As JsonFieldType

        Friend Function HasKnownWireType() As Boolean
            Return Me.FieldCode <> JsonFieldType.Unknown
        End Function

        ''' <summary>
        ''' Gets whether the current token behaves as an array container.
        ''' Scalar tokens return <c>False</c>.
        ''' </summary>
        Public Overridable ReadOnly Property IsArray As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Gets whether the current token is a primitive scalar.
        ''' Container tokens return <c>False</c>.
        ''' </summary>
        Public Overridable ReadOnly Property IsPrimitive As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Gets whether the current token represents a null value.
        ''' </summary>
        Public Overridable ReadOnly Property IsNull As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' Gets the number of child elements.
        ''' Scalar tokens return <c>0</c>.
        ''' </summary>
        Public Overridable ReadOnly Property Count As Integer
            Get
                Return 0
            End Get
        End Property

        ''' <summary>
        ''' Gets or sets a named child token.
        ''' Only object-like tokens should override this member.
        ''' </summary>
        Default Public Overridable Property Item(name As String) As BToken
            Get
                Throw New InvalidOperationException($"{Me.GetType().Name} does not support string indexing.")
            End Get
            Set(value As BToken)
                Throw New InvalidOperationException($"{Me.GetType().Name} does not support string indexing.")
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets an indexed child token.
        ''' Only array-like tokens should override this member.
        ''' </summary>
        Default Public Overridable Property Item(index As Integer) As BToken
            Get
                Throw New InvalidOperationException($"{Me.GetType().Name} does not support integer indexing.")
            End Get
            Set(value As BToken)
                Throw New InvalidOperationException($"{Me.GetType().Name} does not support integer indexing.")
            End Set
        End Property

        ''' <summary>
        ''' Converts the current token into its CLR representation.
        ''' </summary>
        Public MustOverride Function ToObject() As Object

        ''' <summary>
        ''' Handles conversion of a null token into the requested CLR type.
        '''
        ''' Rules:
        ''' - Reference types and nullable value types receive <c>Nothing</c>.
        ''' - Non-nullable value types throw.
        ''' </summary>
        Protected Shared Function ConvertNullValue(Of T)(tokenName As String) As T
            Dim targetType As Type = GetType(T)

            If Not targetType.IsValueType OrElse Nullable.GetUnderlyingType(targetType) IsNot Nothing Then
                Return CType(Nothing, T)
            End If

            Throw New InvalidCastException($"Cannot convert null {tokenName} to {targetType.FullName}.")
        End Function

#Region "FromObject"

        ''' <summary>
        ''' Converts any CLR value into the corresponding <see cref="BToken"/>.
        '''
        ''' This is the main factory entry point for building the DOM from .NET values.
        ''' </summary>
        Public Shared Function FromObject(value As Object) As BToken
            Return FromObjectCore(value, If(value?.GetType(), Nothing))
        End Function

        ''' <summary>
        ''' Converts a CLR value into a <see cref="BToken"/> while preserving
        ''' the caller-provided declared type context.
        ''' </summary>
        Friend Shared Function FromObject(value As Object, declaredType As Type) As BToken
            Return FromObjectCore(value, declaredType)
        End Function

        ''' <summary>
        ''' Core CLR-to-token conversion routine.
        '''
        ''' Resolution order:
        ''' - existing <see cref="BToken"/>
        ''' - null
        ''' - primitive CLR types
        ''' - dictionaries
        ''' - enumerables
        ''' - plain objects
        ''' </summary>
        Private Shared Function FromObjectCore(value As Object, declaredType As Type) As BToken

            Dim existing As BToken = TryCast(value, BToken)
            If existing IsNot Nothing Then Return existing

            If value Is Nothing Then
                Dim fieldCode As JsonFieldType = DotNet.Cache.TypeToFieldType(declaredType)
                Return BNull.Instance(fieldCode)
            End If

            Dim runtimeType As Type = value.GetType()
            Dim effectiveType As Type = UnwrapNullable(runtimeType)

            If effectiveType Is GetType(String) Then
                Return New BString(DirectCast(value, String))
            End If

            If effectiveType Is GetType(Char) Then
                Return New BString(CChar(value).ToString())
            End If

            If effectiveType Is GetType(Boolean) Then
                Return New BBoolean(CBool(value))
            End If

            If effectiveType Is GetType(Byte()) Then
                Return New BBytes(DirectCast(value, Byte()))
            End If

            If effectiveType Is GetType(Date) Then
                Return New BDate(CDate(value))
            End If

            If effectiveType Is GetType(DateTimeOffset) Then
                Return New BDate(DirectCast(value, DateTimeOffset).UtcDateTime)
            End If

            If effectiveType.IsEnum Then
                Return New BNumber(Convert.ToInt64(value, CultureInfo.InvariantCulture))
            End If

            Select Case Type.GetTypeCode(effectiveType)
                Case TypeCode.Byte,
                         TypeCode.SByte,
                         TypeCode.Int16,
                         TypeCode.UInt16,
                         TypeCode.Int32,
                         TypeCode.UInt32,
                         TypeCode.Int64,
                         TypeCode.UInt64
                    Return New BNumber(Convert.ToInt64(value, CultureInfo.InvariantCulture))

                Case TypeCode.Single
                    Return New BNumber(Convert.ToSingle(value, CultureInfo.InvariantCulture))

                Case TypeCode.Double, TypeCode.Decimal
                    Return New BNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture))
            End Select

            If TypeOf value Is IDictionary Then
                Return BObject.FromDictionary(DirectCast(value, IDictionary), declaredType)
            End If

            If IsDictionaryLike(runtimeType) Then
                Return BObject.FromAnyDictionary(value, declaredType)
            End If

            If IsEnumerableLike(runtimeType) Then
                Return BArray.FromEnumerable(DirectCast(value, IEnumerable), declaredType)
            End If

            Return BObject.FromPlainObject(value, declaredType)

        End Function

        ''' <summary>
        ''' Removes <see cref="Nullable(Of T)"/> wrapping when present.
        ''' </summary>
        Private Shared Function UnwrapNullable(t As Type) As Type
            If t Is Nothing Then Return Nothing
            Return If(Nullable.GetUnderlyingType(t), t)
        End Function

        ''' <summary>
        ''' Returns whether the supplied CLR type behaves like a dictionary.
        '''
        ''' Supported shapes:
        ''' - non-generic <see cref="IDictionary"/>
        ''' - generic <c>IDictionary(Of K, V)</c>
        ''' - generic <c>IReadOnlyDictionary(Of K, V)</c>
        ''' </summary>
        Private Shared Function IsDictionaryLike(t As Type) As Boolean

            If t Is Nothing Then Return False
            If GetType(IDictionary).IsAssignableFrom(t) Then Return True

            For Each itf As Type In t.GetInterfaces()
                If itf.IsGenericType Then
                    Dim gtd As Type = itf.GetGenericTypeDefinition()

                    If gtd Is GetType(IDictionary(Of ,)) OrElse
               gtd Is GetType(IReadOnlyDictionary(Of ,)) Then
                        Return True
                    End If
                End If
            Next

            Return False

        End Function

        ''' <summary>
        ''' Returns whether the CLR type should be treated as numeric.
        ''' </summary>
        Private Shared Function IsNumericClrType(t As Type) As Boolean

            If t Is Nothing Then Return False

            Select Case Type.GetTypeCode(t)
                Case TypeCode.Byte,
                     TypeCode.SByte,
                     TypeCode.Int16,
                     TypeCode.UInt16,
                     TypeCode.Int32,
                     TypeCode.UInt32,
                     TypeCode.Int64,
                     TypeCode.UInt64,
                     TypeCode.Single,
                     TypeCode.Double,
                     TypeCode.Decimal
                    Return True
                Case Else
                    Return False
            End Select

        End Function

        ''' <summary>
        ''' Returns whether the CLR type should be treated as an array-like token source.
        '''
        ''' Exclusions:
        ''' - <see cref="String"/>
        ''' - <c>Byte()</c>
        ''' - dictionary-like types
        ''' </summary>
        Private Shared Function IsEnumerableLike(t As Type) As Boolean

            If t Is Nothing Then Return False
            If t Is GetType(String) Then Return False
            If t Is GetType(Byte()) Then Return False
            If GetType(IDictionary).IsAssignableFrom(t) Then Return False

            Return GetType(IEnumerable).IsAssignableFrom(t)

        End Function

#End Region

#Region "GetValue"

        ''' <summary>
        ''' Converts the current token directly to <typeparamref name="T"/>.
        ''' </summary>
        Public MustOverride Overloads Function GetValue(Of T)() As T

        ''' <summary>
        ''' Internal recursive path resolver used by dotted-path accessors.
        ''' </summary>
        Friend MustOverride Overloads Function GetValue(Of T)(paths As String(), pathIndex As Integer, pathOptional As Boolean, [default] As T) As T

        ''' <summary>
        ''' Resolves a dotted path and returns the target token.
        ''' </summary>
        Public Overloads Function GetValue(path As String) As BToken
            Return ResolvePathValue(Of BToken)(path, forceOptional:=False, [default]:=Nothing)
        End Function

        ''' <summary>
        ''' Resolves a dotted path and converts the result to <typeparamref name="T"/>.
        ''' </summary>
        Public Overloads Function GetValue(Of T)(path As String) As T
            Return ResolvePathValue(Of T)(path, forceOptional:=False, [default]:=Nothing)
        End Function

        ''' <summary>
        ''' Resolves a dotted path and returns a caller-supplied default when appropriate.
        ''' </summary>
        Public Overloads Function GetValue(Of T)(path As String, [default] As T) As T
            Return ResolvePathValue(Of T)(path, forceOptional:=False, [default]:=[default])
        End Function

        ''' <summary>
        ''' Tries to resolve a dotted path without throwing for missing optional segments.
        '''
        ''' This behaves as though the path started with the optional prefix <c>*</c>.
        ''' </summary>
        Public Overloads Function TryGetValue(path As String, ByRef result As BToken) As Boolean
            result = ResolvePathValue(Of BToken)(path, forceOptional:=True, [default]:=Nothing)
            Return result IsNot Nothing AndAlso Not result.IsNull
        End Function

        ''' <summary>
        ''' Shared path-resolution helper used by all public path APIs.
        '''
        ''' Optional path syntax:
        ''' - a leading <c>*</c> marks the path as optional
        ''' - missing values then return the provided default instead of failing
        ''' </summary>
        Private Function ResolvePathValue(Of T)(path As String, forceOptional As Boolean, [default] As T) As T

            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException(NameOf(path) & " cannot be null or empty.", NameOf(path))
            End If

            Dim pathOptional As Boolean = forceOptional

            If path.StartsWith("*", StringComparison.Ordinal) Then
                pathOptional = True
                path = path.Substring(1)
            End If

            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException("Path cannot be empty after removing optional prefix '*'.", NameOf(path))
            End If

            Dim paths() As String = path.Split("."c)
            Return Me.GetValue(Of T)(paths, 0, pathOptional, [default])

        End Function

#End Region

#Region "Json"

        ''' <summary>
        ''' Serializes the current token to JSON text.
        ''' </summary>
        Public Function ToJson(Optional indent As Boolean = False) As String
            Return ToJsonString(indent, 0)
        End Function

        ''' <summary>
        ''' Internal JSON writer used recursively by container tokens.
        ''' </summary>
        Friend MustOverride Function ToJsonString(indent As Boolean, indentCount As Integer) As String

        ''' <summary>
        ''' Escapes a CLR string as a JSON string literal.
        '''
        ''' Returned format:
        ''' - includes the outer double quotes
        ''' - returns literal <c>"null"</c> when the input string reference is <c>Nothing</c>
        ''' </summary>
        Protected Shared Function EscapeJsonString(value As String) As String

            If value Is Nothing Then
                Return "null"
            End If

            Dim sb As New StringBuilder(value.Length + 8)
            sb.Append(""""c)

            For Each ch As Char In value
                Select Case ch
                    Case """"c : sb.Append("\""")
                    Case "\"c : sb.Append("\\")
                    Case ControlChars.Back : sb.Append("\b")
                    Case ControlChars.FormFeed : sb.Append("\f")
                    Case ControlChars.Cr : sb.Append("\r")
                    Case ControlChars.Lf : sb.Append("\n")
                    Case ControlChars.Tab : sb.Append("\t")
                    Case Else
                        Dim code As Integer = AscW(ch)
                        If code < 32 Then
                            sb.Append("\u")
                            sb.Append(code.ToString("x4", CultureInfo.InvariantCulture))
                        Else
                            sb.Append(ch)
                        End If
                End Select
            Next

            sb.Append(""""c)
            Return sb.ToString()

        End Function

        ''' <summary>
        ''' Returns the compact string representation of the token.
        ''' </summary>
        Public MustOverride Overrides Function ToString() As String

        ''' <summary>
        ''' Returns the token string representation with optional indentation.
        ''' </summary>
        Public MustOverride Overloads Function ToString(indent As Boolean) As String

#End Region

        ''' <summary>
        ''' Parses JSON text into a <see cref="BToken"/> tree.
        ''' </summary>
        Public Shared Function ParseJSON(json As String) As BToken
            Return BTokenParser.Parse(json)
        End Function

        ''' <summary>
        ''' Parses Bytery bytes into a <see cref="BToken"/> tree.
        ''' </summary>
        Public Shared Function ParseBytery(data As Byte(), Optional ByRef headers As List(Of HeaderEntry) = Nothing) As BToken
            Return Bytery.Decode(data, False, False, headers)
        End Function

    End Class

End Namespace