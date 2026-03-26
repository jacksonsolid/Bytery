Imports System.Collections.Concurrent
Imports System.Threading

Namespace JSON

    ''' <summary>
    ''' Process-wide cache of canonical <see cref="JsonSchema"/> instances, indexed by schema key.
    ''' </summary>
    ''' <remarks>
    ''' This cache guarantees that each logical schema key is materialized at most once,
    ''' even under concurrent access.
    '''
    ''' Implementation notes:
    ''' - The outer dictionary is thread-safe.
    ''' - Each entry stores a <see cref="Lazy(Of T)"/> so that the factory is executed once.
    ''' - Keys are compared with <see cref="StringComparer.Ordinal"/> because schema keys are
    '''   protocol identities, not culture-aware text.
    ''' </remarks>
    Friend NotInheritable Class Cache

        ''' <summary>
        ''' Static-only type.
        ''' </summary>
        Private Sub New()
        End Sub

        ''' <summary>
        ''' Global schema cache indexed by canonical schema key.
        ''' </summary>
        ''' <remarks>
        ''' The cached value is wrapped in <see cref="Lazy(Of JsonSchema)"/> so that
        ''' concurrent callers racing on the same key still converge on a single schema instance.
        ''' </remarks>
        Private Shared ReadOnly _byKey As New ConcurrentDictionary(Of String, Lazy(Of JsonSchema))(StringComparer.Ordinal)

        ''' <summary>
        ''' Gets an existing schema for the specified key or creates it atomically.
        ''' </summary>
        ''' <param name="key">Canonical schema key.</param>
        ''' <param name="factory">Factory used to build the schema when the key is not cached yet.</param>
        ''' <returns>The cached or newly created <see cref="JsonSchema"/> instance.</returns>
        ''' <remarks>
        ''' The factory is wrapped in <see cref="Lazy(Of T)"/> using
        ''' <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, which means:
        '''
        ''' - Only one thread executes the factory.
        ''' - The resulting schema is published to all other threads.
        ''' - If multiple callers race on the same key, they all receive the same resolved instance.
        ''' </remarks>
        Public Shared Function GetOrAdd(key As String, factory As Func(Of JsonSchema)) As JsonSchema
            If String.IsNullOrEmpty(key) Then Throw New ArgumentNullException(NameOf(key))
            If factory Is Nothing Then Throw New ArgumentNullException(NameOf(factory))

            Dim lazy = _byKey.GetOrAdd(
                key,
                Function(k As String)
                    Return New Lazy(Of JsonSchema)(
                        Function() factory(),
                        LazyThreadSafetyMode.ExecutionAndPublication
                    )
                End Function
            )

            Return lazy.Value
        End Function

        ''' <summary>
        ''' Convenience overload that caches a schema using a <see cref="DotNet.DotNetSchema"/> source.
        ''' </summary>
        ''' <param name="dn">Reflection-side schema whose key will be used as the cache identity.</param>
        ''' <param name="factory">Factory that converts the DotNet schema into a <see cref="JsonSchema"/>.</param>
        ''' <returns>The cached or newly created <see cref="JsonSchema"/> instance.</returns>
        ''' <remarks>
        ''' This overload is typically used when the caller already has a <see cref="DotNet.DotNetSchema"/>
        ''' and wants to materialize its wire-facing <see cref="JsonSchema"/> only once.
        ''' </remarks>
        Public Shared Function GetOrAdd(dn As DotNet.DotNetSchema, factory As Func(Of DotNet.DotNetSchema, JsonSchema)) As JsonSchema
            If dn Is Nothing Then Throw New ArgumentNullException(NameOf(dn))
            If factory Is Nothing Then Throw New ArgumentNullException(NameOf(factory))
            Return GetOrAdd(dn.JsonSchemaKey, Function() factory(dn))
        End Function

        ''' <summary>
        ''' Attempts to retrieve a cached schema by key.
        ''' </summary>
        ''' <param name="key">Canonical schema key.</param>
        ''' <param name="schema">
        ''' When this method returns <c>True</c>, contains the resolved cached schema;
        ''' otherwise contains <c>Nothing</c>.
        ''' </param>
        ''' <returns><c>True</c> when the schema exists in the cache; otherwise <c>False</c>.</returns>
        ''' <remarks>
        ''' Accessing <c>lazy.Value</c> resolves the underlying schema if the entry exists
        ''' but has not been materialized yet.
        ''' </remarks>
        Public Shared Function TryGet(key As String, ByRef schema As JsonSchema) As Boolean
            schema = Nothing
            If String.IsNullOrEmpty(key) Then Return False
            Dim lazy As Lazy(Of JsonSchema) = Nothing
            If Not _byKey.TryGetValue(key, lazy) Then Return False
            schema = lazy.Value
            Return True
        End Function

    End Class

End Namespace