using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tortuga.Chain.CommandBuilders;

namespace Tortuga.Chain.Materializers
{
    /// <summary>
    /// Materializes the result set as a collection of the indicated type.
    /// </summary>
    /// <typeparam name="TCommand">The type of the t command type.</typeparam>
    /// <typeparam name="TParameter">The type of the t parameter type.</typeparam>
    /// <typeparam name="TObject">The type of the object returned.</typeparam>
    /// <typeparam name="TCollection">The type of the collection.</typeparam>
    /// <seealso cref="Materializer{TCommand, TParameter, TCollection}" />
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes")]
    internal sealed class CollectionMaterializer<TCommand, TParameter, TObject, TCollection> : ConstructibleMaterializer<TCommand, TParameter, TCollection, TObject>
        where TCommand : DbCommand
        where TObject : class
        where TCollection : ICollection<TObject>, new()
        where TParameter : DbParameter
    {

        readonly CollectionOptions m_CollectionOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionMaterializer{TCommand, TParameter, TObject, TCollection}"/> class.
        /// </summary>
        /// <param name="commandBuilder">The associated operation.</param>
        /// <param name="collectionOptions">The collection options.</param>
        public CollectionMaterializer(DbCommandBuilder<TCommand, TParameter> commandBuilder, CollectionOptions collectionOptions)
            : base(commandBuilder)
        {
            m_CollectionOptions = collectionOptions;


            if (m_CollectionOptions.HasFlag(CollectionOptions.InferConstructor))
            {
                var constructors = ObjectMetadata.Constructors.Where(x => x.Signature.Length > 0).ToList();
                if (constructors.Count == 0)
                    throw new MappingException($"Type {typeof(TObject).Name} has does not have any non-default constructors.");
                if (constructors.Count > 1)
                    throw new MappingException($"Type {typeof(TObject).Name} has more than one non-default constructor. Please use the WithConstructor method to specify which one to use.");
                ConstructorSignature = constructors[0].Signature;
            }
        }

        /// <summary>
        /// Execute the operation synchronously.
        /// </summary>
        /// <returns></returns>
        public override TCollection Execute(object state = null)
        {
            var result = new TCollection();
            Prepare().Execute(cmd =>
            {
                using (var reader = cmd.ExecuteReader().AsObjectConstructor<TObject>(ConstructorSignature))
                {
                    while (reader.Read())
                        result.Add(reader.Current);
                    return result.Count;
                }
            }, state);

            return result;
        }


        /// <summary>
        /// Execute the operation asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="state">User defined state, usually used for logging.</param>
        /// <returns></returns>
        public override async Task<TCollection> ExecuteAsync(CancellationToken cancellationToken, object state = null)
        {
            var result = new TCollection();

            await Prepare().ExecuteAsync(async cmd =>
            {
                using (var reader = (await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)).AsObjectConstructor<TObject>(ConstructorSignature))
                {
                    while (await reader.ReadAsync())
                        result.Add(reader.Current);
                    return result.Count;
                }
            }, cancellationToken, state).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Returns the list of columns the materializer would like to have.
        /// </summary>
        /// <returns></returns>
        public override IReadOnlyList<string> DesiredColumns()
        {
            if (ConstructorSignature == null)
                return ObjectMetadata.ColumnsFor;

            var desiredType = typeof(TObject);
            var constructor = ObjectMetadata.Constructors.Find(ConstructorSignature);

            if (constructor == null)
            {
                var types = string.Join(", ", ConstructorSignature.Select(t => t.Name));
                throw new MappingException($"Cannot find a constructor on {desiredType.Name} with the types [{types}]");
            }

            return constructor.ParameterNames;
        }
    }
}
