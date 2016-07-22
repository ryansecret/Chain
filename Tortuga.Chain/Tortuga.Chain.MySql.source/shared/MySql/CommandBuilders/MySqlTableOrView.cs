﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using Tortuga.Chain.CommandBuilders;
using Tortuga.Chain.Core;
using Tortuga.Chain.Materializers;
using Tortuga.Chain.Metadata;

namespace Tortuga.Chain.MySql.CommandBuilders
{

    /// <summary>
    /// Class MySqlTableOrView
    /// </summary>
    public class MySqlTableOrView : TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption>
    {
        readonly TableOrViewMetadata<MySqlObjectName, MySqlDbType> m_Table;
        private object m_FilterValue;
        private string m_WhereClause;
        private object m_ArgumentValue;

        private IEnumerable<SortExpression> m_SortExpressions = Enumerable.Empty<SortExpression>();
        private MySqlLimitOption m_LimitOptions;
        private int? m_Skip;
        private int? m_Take;
        private int? m_Seed;
        private string m_SelectClause;

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlTableOrView" /> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <exception cref="ArgumentException"></exception>
        public MySqlTableOrView(MySqlDataSourceBase dataSource, MySqlObjectName tableOrViewName) :
            base(dataSource)
        {
            if (tableOrViewName == MySqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_Table = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlTableOrView" /> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <param name="filterValue">The filter value.</param>
        /// <exception cref="ArgumentException"></exception>
        public MySqlTableOrView(MySqlDataSourceBase dataSource, MySqlObjectName tableOrViewName, object filterValue) :
            base(dataSource)
        {
            if (tableOrViewName == MySqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_FilterValue = filterValue;
            m_Table = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlTableOrView"/> class.
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="tableOrViewName">Name of the table or view.</param>
        /// <param name="whereClause">The where clause.</param>
        /// <param name="argumentValue">The argument value.</param>
        /// <exception cref="ArgumentException"></exception>
        public MySqlTableOrView(MySqlDataSourceBase dataSource, MySqlObjectName tableOrViewName, string whereClause, object argumentValue)
            : base(dataSource)
        {
            if (tableOrViewName == MySqlObjectName.Empty)
                throw new ArgumentException($"{nameof(tableOrViewName)} is empty", nameof(tableOrViewName));

            m_WhereClause = whereClause;
            m_ArgumentValue = argumentValue;
            m_Table = DataSource.DatabaseMetadata.GetTableOrView(tableOrViewName);
        }

        /// <summary>
        /// Prepares the command for execution by generating any necessary SQL.
        /// </summary>
        /// <param name="materializer">The materializer.</param>
        /// <returns>
        /// ExecutionToken&lt;TCommand&gt;.
        /// </returns>
        public override CommandExecutionToken<MySqlCommand, MySqlParameter> Prepare(Materializer<MySqlCommand, MySqlParameter> materializer)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Adds sorting to the command builder.
        /// </summary>
        /// <param name="sortExpressions">The sort expressions.</param>
        /// <returns></returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithSorting(IEnumerable<SortExpression> sortExpressions)
        {
            if (sortExpressions == null)
                throw new ArgumentNullException(nameof(sortExpressions), $"{nameof(sortExpressions)} is null.");

            m_SortExpressions = sortExpressions;
            return this;
        }

        /// <summary>
        /// Adds limits to the command builder.
        /// </summary>
        /// <param name="skip">The number of rows to skip.</param>
        /// <param name="take">Number of rows to take.</param>
        /// <param name="limitOptions">The limit options.</param>
        /// <param name="seed">The seed for repeatable reads. Only applies to random sampling</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        protected override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> OnWithLimits(int? skip, int? take, MySqlLimitOption limitOptions, int? seed)
        {
            m_Seed = seed;
            m_Skip = skip;
            m_Take = take;
            m_LimitOptions = limitOptions;
            return this;
        }

        /// <summary>
        /// Adds limits to the command builder.
        /// </summary>
        /// <param name="skip">The number of rows to skip.</param>
        /// <param name="take">Number of rows to take.</param>
        /// <param name="limitOptions">The limit options.</param>
        /// <param name="seed">The seed for repeatable reads. Only applies to random sampling</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        protected override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> OnWithLimits(int? skip, int? take, LimitOptions limitOptions, int? seed)
        {
            m_Seed = seed;
            m_Skip = skip;
            m_Take = take;
            m_LimitOptions = (MySqlLimitOption)limitOptions;
            return this;
        }

        /// <summary>
        /// Adds (or replaces) the filter on this command builder.
        /// </summary>
        /// <param name="filterValue">The filter value.</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithFilter(object filterValue)
        {
            m_FilterValue = filterValue;
            m_WhereClause = null;
            m_ArgumentValue = null;
            return this;
        }

        /// <summary>
        /// Adds (or replaces) the filter on this command builder.
        /// </summary>
        /// <param name="whereClause">The where clause.</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithFilter(string whereClause)
        {
            m_FilterValue = null;
            m_WhereClause = whereClause;
            m_ArgumentValue = null;
            return this;
        }

        /// <summary>
        /// Adds (or replaces) the filter on this command builder.
        /// </summary>
        /// <param name="whereClause">The where clause.</param>
        /// <param name="argumentValue">The argument value.</param>
        /// <returns>TableDbCommandBuilder&lt;MySqlCommand, MySqlParameter, MySqlLimitOption&gt;.</returns>
        public override TableDbCommandBuilder<MySqlCommand, MySqlParameter, MySqlLimitOption> WithFilter(string whereClause, object argumentValue)
        {
            m_FilterValue = null;
            m_WhereClause = whereClause;
            m_ArgumentValue = argumentValue;
            return this;
        }

        /// <summary>
        /// Returns the row count using a <c>SELECT COUNT_BIG(*)</c> style query.
        /// </summary>
        /// <returns></returns>
        public override ILink<long> AsCount()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the row count for a given column. <c>SELECT COUNT_BIG(columnName)</c>
        /// </summary>
        /// <param name="columnName">Name of the column.</param>
        /// <param name="distinct">if set to <c>true</c> use <c>SELECT COUNT_BIG(DISTINCT columnName)</c>.</param>
        /// <returns></returns>
        public override ILink<long> AsCount(string columnName, bool distinct = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the data source.
        /// </summary>
        /// <value>The data source.</value>
        public new MySqlDataSourceBase DataSource
        {
            get { return (MySqlDataSourceBase)base.DataSource; }
        }

        /// <summary>
        /// Returns the column associated with the column name.
        /// </summary>
        /// <param name="columnName">Name of the column.</param>
        /// <returns></returns>
        /// <remarks>
        /// If the column name was not found, this will return null
        /// </remarks>
        public override ColumnMetadata TryGetColumn(string columnName)
        {
            return m_Table.Columns.TryGetColumn(columnName);
        }
    }
}
