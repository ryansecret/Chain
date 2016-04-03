﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tortuga.Anchor.Metadata;
using Tortuga.Chain.CommandBuilders;
using Tortuga.Chain.Core;
using Tortuga.Chain.Materializers;
using Tortuga.Chain.Metadata;
using Tortuga.Chain.SqlServer.Core;
using Tortuga.Chain.SqlServer.Materializers;
namespace Tortuga.Chain.SqlServer.CommandBuilders
{
    /// <summary>
    /// SqlServerTableOrView supports queries against tables and views.
    /// </summary>
    internal sealed class SqlServerTableOrView : MultipleRowDbCommandBuilder<SqlCommand, SqlParameter>, ISupportsChangeListener
    {
        private readonly object m_FilterValue;
        private readonly TableOrViewMetadata<SqlServerObjectName, SqlDbType> m_Metadata;
        private readonly string m_WhereClause;
        private readonly object m_ArgumentValue;


        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerTableOrView"/> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <param name="filterValue">The filter value.</param>
        public SqlServerTableOrView(SqlServerDataSourceBase dataSource, SqlServerObjectName tableOrViewName, object filterValue) : base(dataSource)
        {
            if (tableOrViewName == SqlServerObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_FilterValue = filterValue;
            m_Metadata = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerTableOrView"/> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <param name="whereClause">The where clause.</param>
        /// <param name="argumentValue">The argument value.</param>
        public SqlServerTableOrView(SqlServerDataSourceBase dataSource, SqlServerObjectName tableOrViewName, string whereClause, object argumentValue) : base(dataSource)
        {
            if (tableOrViewName == SqlServerObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_ArgumentValue = argumentValue;
            m_WhereClause = whereClause;
            m_Metadata = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Prepares the command for execution by generating any necessary SQL.
        /// </summary>
        /// <param name="materializer">The materializer.</param>
        public override ExecutionToken<SqlCommand, SqlParameter> Prepare(Materializer<SqlCommand, SqlParameter> materializer)
        {
            var sqlBuilder = m_Metadata.CreateSqlBuilder();
            sqlBuilder.ApplyDesiredColumns(materializer.DesiredColumns(), DataSource.StrictMode);

            var parameters = new List<SqlParameter>();

            var sql = new StringBuilder();
            sqlBuilder.BuildSelectClause(sql, "SELECT ", null , " FROM " + m_Metadata.Name.ToQuotedString());

            if (m_FilterValue != null)
                sql.Append( WhereClauseA(parameters));
            else if (!string.IsNullOrWhiteSpace(m_WhereClause))
                sql.Append(WhereClauseB(parameters));
            sql.Append(";");

            return new SqlServerExecutionToken(DataSource, "Query " + m_Metadata.Name, sql.ToString(), parameters);
        }

        private string WhereClauseA(List<SqlParameter> parameters)
        {
            var availableColumns = m_Metadata.Columns.ToDictionary(c => c.ClrName, StringComparer.OrdinalIgnoreCase);
            var properties = MetadataCache.GetMetadata(m_FilterValue.GetType()).Properties;
            var actualColumns = new List<string>();

            if (m_FilterValue is IReadOnlyDictionary<string, object>)
            {
                foreach (var item in (IReadOnlyDictionary<string, object>)m_FilterValue)
                {
                    ColumnMetadata<SqlDbType> column;
                    if (availableColumns.TryGetValue(item.Key, out column))
                    {
                        object value = item.Value ?? DBNull.Value;
                        var parameter = new SqlParameter(column.SqlVariableName, value);
                        if (column.DbType.HasValue)
                            parameter.SqlDbType = column.DbType.Value;

                        if (value == DBNull.Value)
                        {
                            actualColumns.Add($"{column.QuotedSqlName} IS NULL");
                        }
                        else
                        {
                            actualColumns.Add($"{column.QuotedSqlName} = {column.SqlVariableName}");
                            parameters.Add(parameter);
                        }
                    }
                }
            }
            else
            {
                foreach (var item in properties)
                {
                    ColumnMetadata<SqlDbType> column;
                    if (availableColumns.TryGetValue(item.MappedColumnName, out column))
                    {
                        object value = item.InvokeGet(m_FilterValue) ?? DBNull.Value;
                        var parameter = new SqlParameter(column.SqlVariableName, value);
                        if (column.DbType.HasValue)
                            parameter.SqlDbType = column.DbType.Value;

                        if (value == DBNull.Value)
                        {
                            actualColumns.Add($"{column.QuotedSqlName} IS NULL");
                        }
                        else
                        {
                            actualColumns.Add($"{column.QuotedSqlName} = {column.SqlVariableName}");
                            parameters.Add(parameter);
                        }
                    }
                }
            }

            if (actualColumns.Count == 0)
                throw new MappingException($"Unable to find any properties on type {m_FilterValue.GetType().Name} that match the columns on {m_Metadata.Name}");

            return "WHERE " + string.Join(" AND ", actualColumns);
        }

        private string WhereClauseB(List<SqlParameter> parameters)
        {
            if (m_ArgumentValue is IEnumerable<SqlParameter>)
                foreach (var param in (IEnumerable<SqlParameter>)m_ArgumentValue)
                    parameters.Add(param);
            else if (m_ArgumentValue is IReadOnlyDictionary<string, object>)
                foreach (var item in (IReadOnlyDictionary<string, object>)m_ArgumentValue)
                    parameters.Add(new SqlParameter("@" + item.Key, item.Value ?? DBNull.Value));
            else if (m_ArgumentValue != null)
                foreach (var property in MetadataCache.GetMetadata(m_ArgumentValue.GetType()).Properties)
                    parameters.Add(new SqlParameter("@" + property.MappedColumnName, property.InvokeGet(m_ArgumentValue) ?? DBNull.Value));

            return "WHERE " + m_WhereClause;
        }



        /// <summary>
        /// Waits for change in the data that is returned by this operation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="state">User defined state, usually used for logging.</param>
        /// <returns>Task that can be waited for.</returns>
        /// <remarks>This requires the use of SQL Dependency</remarks>
        public Task WaitForChange(CancellationToken cancellationToken, object state = null)
        {
            return WaitForChangeMaterializer.GenerateTask(this, cancellationToken, state);
        }

        SqlServerExecutionToken ISupportsChangeListener.Prepare(Materializer<SqlCommand, SqlParameter> materializer)
        {
            return (SqlServerExecutionToken)Prepare(materializer);
        }

        /// <summary>
        /// Gets the data source.
        /// </summary>
        /// <value>The data source.</value>
        public new SqlServerDataSourceBase DataSource
        {
            get { return (SqlServerDataSourceBase)base.DataSource; }
        }
    }
}


