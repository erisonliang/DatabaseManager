﻿using Antlr4.Runtime.Tree;
using SqlAnalyser.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static PlSqlParser;
using Antlr4.Runtime;
using DatabaseInterpreter.Model;

namespace SqlAnalyser.Core
{
    public class PlSqlRuleAnalyser : SqlRuleAnalyser
    {
        public override Lexer GetLexer(string content)
        {
            return new PlSqlLexer(this.GetCharStreamFromString(content));
        }

        public override Parser GetParser(CommonTokenStream tokenStream)
        {
            return new PlSqlParser(tokenStream);
        }

        public Sql_scriptContext GetRootContext(string content, out SqlSyntaxError error)
        {
            error = null;

            PlSqlParser parser = this.GetParser(content) as PlSqlParser;

            SqlSyntaxErrorListener errorListener = new SqlSyntaxErrorListener();

            parser.AddErrorListener(errorListener);

            Sql_scriptContext context = parser.sql_script();

            error = errorListener.Error;

            return context;
        }

        public Unit_statementContext GetUnitStatementContext(string content, out SqlSyntaxError error)
        {
            error = null;

            Sql_scriptContext rootContext = this.GetRootContext(content, out error);

            return rootContext?.unit_statement()?.FirstOrDefault();
        }

        public override AnalyseResult AnalyseProcedure(string content)
        {
            SqlSyntaxError error = null;

            Unit_statementContext unitStatement = this.GetUnitStatementContext(content, out error);

            AnalyseResult result = new AnalyseResult() { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                RoutineScript script = new RoutineScript() { Type = RoutineType.PROCEDURE };

                Create_procedure_bodyContext proc = unitStatement.create_procedure_body();

                if (proc != null)
                {
                    #region Name
                    Procedure_nameContext name = proc.procedure_name();

                    if (name.id_expression() != null)
                    {
                        script.Owner = new TokenInfo(name.identifier());
                        script.Name = new TokenInfo(name.id_expression());
                    }
                    else
                    {
                        script.Name = new TokenInfo(name.identifier());
                    }
                    #endregion

                    #region Parameters   
                    this.SetRoutineParameters(script, proc.parameter());
                    #endregion

                    #region Declare
                    var declare = proc.seq_of_declare_specs();

                    if (declare != null)
                    {
                        script.Statements.AddRange(declare.declare_spec().Select(item => this.ParseDeclareStatement(item)));
                    }
                    #endregion

                    #region Body
                    this.SetScriptBody(script, proc.body());
                    #endregion
                }

                this.ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        public override AnalyseResult AnalyseFunction(string content)
        {
            SqlSyntaxError error = null;

            Unit_statementContext unitStatement = this.GetUnitStatementContext(content, out error);

            AnalyseResult result = new AnalyseResult() { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                RoutineScript script = new RoutineScript() { Type = RoutineType.FUNCTION };

                Create_function_bodyContext func = unitStatement.create_function_body();

                if (func != null)
                {
                    #region Name
                    Function_nameContext name = func.function_name();

                    if (name.id_expression() != null)
                    {
                        script.Owner = new TokenInfo(name.identifier());
                        script.Name = new TokenInfo(name.id_expression());
                    }
                    else
                    {
                        script.Name = new TokenInfo(name.identifier());
                    }
                    #endregion

                    #region Parameters
                    this.SetRoutineParameters(script, func.parameter());
                    #endregion

                    #region Declare
                    var declare = func.seq_of_declare_specs();

                    if (declare != null)
                    {
                        script.Statements.AddRange(declare.declare_spec().Select(item => this.ParseDeclareStatement(item)));
                    }
                    #endregion

                    script.ReturnDataType = new TokenInfo(func.type_spec().GetText()) { Type = TokenType.DataType };

                    #region Body
                    this.SetScriptBody(script, func.body());
                    #endregion
                }

                this.ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        public override AnalyseResult AnalyseView(string content)
        {
            SqlSyntaxError error = null;

            Unit_statementContext unitStatement = this.GetUnitStatementContext(content, out error);

            AnalyseResult result = new AnalyseResult() { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                ViewScript script = new ViewScript();

                Create_viewContext view = unitStatement.create_view();

                if (view != null)
                {
                    #region Name
                    Tableview_nameContext name = view.tableview_name();

                    if (name.id_expression() != null)
                    {
                        script.Owner = new TokenInfo(name.identifier());
                        script.Name = new TokenInfo(name.id_expression());
                    }
                    else
                    {
                        script.Name = new TokenInfo(name.identifier());
                    }
                    #endregion                  

                    #region Statement

                    foreach (var child in view.children)
                    {
                        if (child is Select_only_statementContext select)
                        {
                            script.Statements.Add(this.ParseSelectOnlyStatement(select));
                        }
                    }

                    #endregion
                }

                this.ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        public override AnalyseResult AnalyseTrigger(string content)
        {
            SqlSyntaxError error = null;

            Unit_statementContext unitStatement = this.GetUnitStatementContext(content, out error);

            AnalyseResult result = new AnalyseResult() { Error = error };

            if (!result.HasError && unitStatement != null)
            {
                TriggerScript script = new TriggerScript();

                Create_triggerContext trigger = unitStatement.create_trigger();

                if (trigger != null)
                {
                    #region Name

                    Trigger_nameContext name = trigger.trigger_name();

                    if (name.id_expression() != null)
                    {
                        script.Owner = new TokenInfo(name.identifier());
                        script.Name = new TokenInfo(name.id_expression());
                    }
                    else
                    {
                        script.Name = new TokenInfo(name.identifier());
                    }

                    #endregion

                    Simple_dml_triggerContext simpleDml = trigger.simple_dml_trigger();

                    if (simpleDml != null)
                    {
                        Tableview_nameContext tableName = simpleDml.dml_event_clause().tableview_name();
                        script.TableName = new TokenInfo(tableName) { Type = TokenType.TableName };

                        Dml_event_elementContext[] events = simpleDml.dml_event_clause().dml_event_element();

                        foreach (Dml_event_elementContext evt in events)
                        {
                            TriggerEvent triggerEvent = (TriggerEvent)Enum.Parse(typeof(TriggerEvent), evt.GetText());

                            script.Events.Add(triggerEvent);
                        }

                        foreach (var child in trigger.children)
                        {
                            if (child is TerminalNodeImpl terminalNode)
                            {
                                switch (terminalNode.Symbol.Type)
                                {
                                    case PlSqlParser.BEFORE:
                                        script.Time = TriggerTime.BEFORE;
                                        break;
                                    case PlSqlParser.AFTER:
                                        script.Time = TriggerTime.AFTER;
                                        break;
                                    case PlSqlParser.INSTEAD:
                                        script.Time = TriggerTime.INSTEAD_OF;
                                        break;
                                }
                            }
                        }
                    }

                    ConditionContext condition = trigger.trigger_when_clause()?.condition();

                    if (condition != null)
                    {
                        script.Condition = new TokenInfo(condition) { Type = TokenType.Condition };
                    }

                    #region Body

                    Trigger_bodyContext triggerBody = trigger.trigger_body();
                    Trigger_blockContext block = triggerBody.trigger_block();

                    Declare_specContext[] declares = block.declare_spec();

                    if (declares != null && declares.Length > 0)
                    {
                        script.Statements.AddRange(declares.Select(item => this.ParseDeclareStatement(item)));
                    }

                    this.SetScriptBody(script, block.body());

                    #endregion
                }

                this.ExtractFunctions(script, unitStatement);

                result.Script = script;
            }

            return result;
        }

        public void SetScriptBody(CommonScript script, BodyContext body)
        {
            script.Statements.AddRange(this.ParseBody(body));
        }

        public List<Statement> ParseBody(BodyContext node)
        {
            List<Statement> statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Seq_of_statementsContext seq)
                {
                    statements.AddRange(this.ParseSeqStatement(seq));
                }
            }

            if (node.exception_handler()?.Any() == true)
            {
                statements.Add(this.ParseException(node));
            }

            return statements;
        }

        public void SetRoutineParameters(RoutineScript script, ParameterContext[] parameters)
        {
            if (parameters != null)
            {
                foreach (ParameterContext parameter in parameters)
                {
                    Parameter parameterInfo = new Parameter();

                    Parameter_nameContext paraName = parameter.parameter_name();

                    parameterInfo.Name = new TokenInfo(paraName) { Type = TokenType.ParameterName };

                    parameterInfo.DataType = new TokenInfo(parameter.type_spec().GetText()) { Type = TokenType.DataType };

                    Default_value_partContext defaultValue = parameter.default_value_part();

                    if (defaultValue != null)
                    {
                        parameterInfo.DefaultValue = new TokenInfo(defaultValue);
                    }

                    this.SetParameterType(parameterInfo, parameter.children);

                    script.Parameters.Add(parameterInfo);
                }
            }
        }

        public List<Statement> ParseSeqStatement(Seq_of_statementsContext node)
        {
            return node.statement().SelectMany(item => this.ParseStatement(item)).ToList();
        }

        public ExceptionStatement ParseException(BodyContext body)
        {
            ExceptionStatement statement = new ExceptionStatement();

            Exception_handlerContext[] handlers = body.exception_handler();

            if (handlers != null && handlers.Length > 0)
            {
                foreach (Exception_handlerContext handler in handlers)
                {
                    ExceptionItem exceptionItem = new ExceptionItem();
                    exceptionItem.Name = new TokenInfo(handler.exception_name().First());
                    exceptionItem.Statements.AddRange(this.ParseSeqStatement(handler.seq_of_statements()));

                    statement.Items.Add(exceptionItem);
                }
            }

            return statement;
        }

        public void SetParameterType(Parameter parameterInfo, IList<IParseTree> nodes)
        {
            foreach (var child in nodes)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    if (terminalNode.Symbol.Type == PlSqlParser.IN)
                    {
                        parameterInfo.ParameterType = ParameterType.IN;
                    }
                    else if (terminalNode.Symbol.Type == PlSqlParser.OUT)
                    {
                        parameterInfo.ParameterType = ParameterType.OUT;
                    }
                    else if (terminalNode.Symbol.Type == PlSqlParser.INOUT)
                    {
                        parameterInfo.ParameterType = ParameterType.IN | ParameterType.OUT;
                    }
                }
            }
        }

        public List<Statement> ParseStatement(StatementContext node)
        {
            List<Statement> statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Sql_statementContext sql)
                {
                    statements.AddRange(this.ParseSqlStatement(sql));
                }
                else if (child is Assignment_statementContext assignment)
                {
                    statements.AddRange(this.ParseSetStatement(assignment));
                }
                else if (child is If_statementContext @if)
                {
                    statements.Add(this.ParseIfStatement(@if));
                }
                else if (child is Case_statementContext @case)
                {
                    statements.Add(this.ParseCaseStatement(@case));
                }
                else if (child is Loop_statementContext loop)
                {
                    statements.Add(this.ParseLoopStatement(loop));
                }
                else if (child is Function_callContext funcCall)
                {
                    statements.Add(this.ParseFunctionCallStatement(funcCall));
                }
                else if (child is Exit_statementContext exit)
                {
                    statements.Add(this.ParseExitStatement(exit));
                }
                else if (child is BodyContext body)
                {
                    statements.AddRange(this.ParseBody(body));
                }
                else if (child is Return_statementContext @return)
                {
                    statements.Add(this.ParseReturnStatement(@return));
                }
            }

            return statements;
        }

        public LoopExitStatement ParseExitStatement(Exit_statementContext node)
        {
            LoopExitStatement statement = new LoopExitStatement();

            statement.Condition = new TokenInfo(node.condition()) { Type = TokenType.Condition };

            return statement;
        }

        public Statement ParseFunctionCallStatement(Function_callContext node)
        {
            Statement statement;

            TokenInfo functionName = new TokenInfo(node.routine_name()) { Type = TokenType.RoutineName };

            if (functionName.Symbol.IndexOf("DBMS_OUTPUT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                statement = new PrintStatement() { Content = new TokenInfo(node.function_argument()) };
            }
            else
            {
                statement = new CallStatement()
                {
                    Name = functionName,
                    Arguments = node.function_argument().argument().Select(item => new TokenInfo(item)).ToList()
                };
            }

            return statement;
        }

        public List<Statement> ParseSqlStatement(Sql_statementContext node)
        {
            List<Statement> statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Data_manipulation_language_statementsContext data)
                {
                    statements.AddRange(this.ParseDataManipulationLanguageStatement(data));
                }
                else if (child is Cursor_manipulation_statementsContext cursor)
                {
                    statements.AddRange(this.ParseCursorManipulationtatement(cursor));
                }
            }

            return statements;
        }

        public List<Statement> ParseDataManipulationLanguageStatement(Data_manipulation_language_statementsContext node)
        {
            List<Statement> statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Select_statementContext select)
                {
                    statements.Add(this.ParseSelectStatement(select));
                }
                else if (child is Insert_statementContext insert)
                {
                    statements.Add(this.ParseInsertStatement(insert));
                }
                else if (child is Update_statementContext update)
                {
                    statements.Add(this.ParseUpdateStatement(update));
                }
                else if (child is Delete_statementContext delete)
                {
                    statements.AddRange(this.ParseDeleteStatement(delete));
                }
            }

            return statements;
        }

        public List<Statement> ParseCursorManipulationtatement(Cursor_manipulation_statementsContext node)
        {
            List<Statement> statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Open_statementContext open)
                {
                    statements.Add(this.ParseOpenCursorStatement(open));
                }
                else if (child is Fetch_statementContext fetch)
                {
                    statements.Add(this.ParseFetchCursorStatement(fetch));
                }
                else if (child is Close_statementContext close)
                {
                    statements.Add(this.ParseCloseCursorStatement(close));
                }
            }

            return statements;
        }

        public OpenCursorStatement ParseOpenCursorStatement(Open_statementContext node)
        {
            OpenCursorStatement statement = new OpenCursorStatement();

            statement.CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName };

            return statement;
        }

        public FetchCursorStatement ParseFetchCursorStatement(Fetch_statementContext node)
        {
            FetchCursorStatement statement = new FetchCursorStatement();

            statement.CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName };
            statement.Variables.AddRange(node.variable_name().Select(item => new TokenInfo(item) { Type = TokenType.VariableName }));

            return statement;
        }

        public CloseCursorStatement ParseCloseCursorStatement(Close_statementContext node)
        {
            CloseCursorStatement statement = new CloseCursorStatement() { IsEnd = true };

            statement.CursorName = new TokenInfo(node.cursor_name()) { Type = TokenType.CursorName };

            return statement;
        }

        public InsertStatement ParseInsertStatement(Insert_statementContext node)
        {
            InsertStatement statement = new InsertStatement();

            Single_table_insertContext single = node.single_table_insert();

            if (single != null)
            {
                foreach (var child in single.children)
                {
                    if (child is Insert_into_clauseContext into)
                    {
                        statement.TableName = this.ParseTableName(into.general_table_ref());

                        foreach (Column_nameContext colName in into.paren_column_list().column_list().column_name())
                        {
                            statement.Columns.Add(this.ParseColumnName(colName));
                        }
                    }
                    else if (child is Values_clauseContext values)
                    {
                        foreach (var v in values.children)
                        {
                            if (v is ExpressionsContext exp)
                            {
                                foreach (var expChild in exp.children)
                                {
                                    if (expChild is ExpressionContext value)
                                    {
                                        TokenInfo valueInfo = new TokenInfo(value);

                                        statement.Values.Add(valueInfo);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return statement;
        }

        public UpdateStatement ParseUpdateStatement(Update_statementContext node)
        {
            UpdateStatement statement = new UpdateStatement();

            General_table_refContext table = node.general_table_ref();

            statement.TableNames.Add(this.ParseTableName(table));

            Update_set_clauseContext set = node.update_set_clause();
            Column_based_update_set_clauseContext[] columnSets = set.column_based_update_set_clause();

            if (columnSets != null)
            {
                foreach (Column_based_update_set_clauseContext colSet in columnSets)
                {
                    statement.SetItems.Add(new NameValueItem()
                    {
                        Name = new TokenInfo(colSet.column_name()) { Type = TokenType.ColumnName },
                        Value = this.ParseToken(colSet.expression())
                    });
                }
            }

            statement.Condition = this.ParseCondition(node.where_clause());

            return statement;
        }

        public List<DeleteStatement> ParseDeleteStatement(Delete_statementContext node)
        {
            List<DeleteStatement> statements = new List<DeleteStatement>();

            DeleteStatement statement = new DeleteStatement();
            statement.TableName = this.ParseTableName(node.general_table_ref());

            statement.Condition = this.ParseCondition(node.where_clause());

            statements.Add(statement);

            return statements;
        }

        public SelectStatement ParseSelectStatement(Select_statementContext node)
        {
            SelectStatement statement = new SelectStatement();

            SelectLimitInfo selectLimitInfo = null;

            foreach (var child in node.children)
            {
                if (child is Select_only_statementContext query)
                {
                    statement = this.ParseSelectOnlyStatement(query);
                }
                else if (child is Offset_clauseContext offset)
                {
                    if (selectLimitInfo == null)
                    {
                        selectLimitInfo = new SelectLimitInfo();
                    }

                    selectLimitInfo.StartRowIndex = new TokenInfo(offset.expression());
                }
                else if (child is Fetch_clauseContext fetch)
                {
                    if (selectLimitInfo == null)
                    {
                        selectLimitInfo = new SelectLimitInfo();
                    }

                    selectLimitInfo.RowCount = new TokenInfo(fetch.expression());
                }
            }

            if (statement != null)
            {
                if (selectLimitInfo != null)
                {
                    statement.LimitInfo = selectLimitInfo;
                }
            }

            return statement;
        }

        public SelectStatement ParseSelectOnlyStatement(Select_only_statementContext node)
        {
            SelectStatement statement = new SelectStatement();

            List<WithStatement> withStatements = null;

            foreach (var child in node.children)
            {
                if (child is SubqueryContext subquery)
                {
                    statement = this.ParseSubQuery(subquery);
                }
                else if (child is Subquery_factoring_clauseContext factor)
                {
                    List<Statement> statements = this.ParseSubQueryFactoringCause(factor);

                    if (statements != null)
                    {
                        withStatements = statements.Where(item => item is WithStatement).Select(item => (WithStatement)item).ToList();
                    }
                }
            }

            if (withStatements != null)
            {
                statement.WithStatements = withStatements;
            }

            return statement;
        }

        public SelectStatement ParseSubQuery(SubqueryContext node)
        {
            SelectStatement statement = null;

            List<Statement> statements = new List<Statement>();

            foreach (var child in node.children)
            {
                if (child is Subquery_basic_elementsContext basic)
                {
                    statement = this.ParseSubQueryBasic(basic);
                }
                else if (child is Subquery_operation_partContext operation)
                {
                    Statement st = this.ParseSubQueryOperation(operation);

                    if (st != null)
                    {
                        statements.Add(st);
                    }
                }
            }

            if (statement != null)
            {
                var unionStatements = statements.Where(item => item is UnionStatement).Select(item => (UnionStatement)item);

                if (unionStatements.Count() > 0)
                {
                    statement.UnionStatements = unionStatements.ToList();
                }
            }

            return statement;
        }

        public List<Statement> ParseSubQueryFactoringCause(Subquery_factoring_clauseContext node)
        {
            List<Statement> statements = null;

            bool isWith = false;

            foreach (var fc in node.children)
            {
                if (fc is TerminalNodeImpl terminalNode)
                {
                    if (terminalNode.Symbol.Type == PlSqlParser.WITH)
                    {
                        isWith = true;
                    }
                }
                else if (fc is Factoring_elementContext fe)
                {
                    if (isWith)
                    {
                        if (statements == null)
                        {
                            statements = new List<Statement>();
                        }

                        WithStatement withStatement = new WithStatement() { SelectStatements = new List<SelectStatement>() };

                        withStatement.Name = new TokenInfo(fe.query_name()) { Type = TokenType.General };

                        withStatement.SelectStatements.Add(this.ParseSubQuery(fe.subquery()));

                        statements.Add(withStatement);
                    }
                }
            }

            return statements;
        }

        public SelectStatement ParseSubQueryBasic(Subquery_basic_elementsContext node)
        {
            SelectStatement statement = new SelectStatement();

            Query_blockContext block = node.query_block();

            List<ColumnName> columnNames = new List<ColumnName>();

            Selected_listContext selectColumns = block.selected_list();

            foreach (Select_list_elementsContext col in selectColumns.select_list_elements())
            {
                columnNames.Add(this.ParseColumnName(col));
            }

            if (columnNames.Count == 0)
            {
                columnNames.Add(this.ParseColumnName(selectColumns));
            }

            statement.Columns = columnNames;

            statement.FromItems = this.ParseFormClause(block.from_clause());

            Into_clauseContext into = block.into_clause();

            if (into != null)
            {
                statement.IntoTableName = new TokenInfo(into.variable_name().First()) { Type = TokenType.TableName };
            }

            Where_clauseContext where = block.where_clause();
            Order_by_clauseContext orderby = block.order_by_clause();
            Group_by_clauseContext groupby = block.group_by_clause();

            if (where != null)
            {
                statement.Where = this.ParseCondition(where.expression());
            }

            if (orderby != null)
            {
                Order_by_elementsContext[] orderbyElements = orderby.order_by_elements();

                if (orderbyElements != null && orderbyElements.Length > 0)
                {
                    statement.OrderBy = orderbyElements.Select(item => this.ParseToken(item, TokenType.OrderBy)).ToList();
                }
            }

            if (groupby != null)
            {
                Group_by_elementsContext[] groupbyElements = groupby.group_by_elements();
                Having_clauseContext having = groupby.having_clause();

                if (groupbyElements != null && groupbyElements.Length > 0)
                {
                    statement.GroupBy = groupbyElements.Select(item => this.ParseToken(item, TokenType.GroupBy)).ToList();
                }

                if (having != null)
                {
                    statement.Having = this.ParseCondition(having.condition());
                }
            }

            return statement;
        }

        public Statement ParseSubQueryOperation(Subquery_operation_partContext node)
        {
            Statement statement = null;

            bool isUnion = false;
            UnionType unionType = UnionType.UNION;

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    int type = terminalNode.Symbol.Type;

                    switch (type)
                    {
                        case TSqlParser.UNION:
                            isUnion = true;
                            break;
                        case TSqlParser.ALL:
                            unionType = UnionType.UNION_ALL;
                            break;
                    }
                }
                else if (child is Subquery_basic_elementsContext basic)
                {
                    if (isUnion)
                    {
                        UnionStatement unionStatement = new UnionStatement();
                        unionStatement.Type = unionType;
                        unionStatement.SelectStatement = this.ParseSubQueryBasic(basic);

                        statement = unionStatement;
                    }
                }
            }

            return statement;
        }

        public List<FromItem> ParseFormClause(From_clauseContext node)
        {
            List<FromItem> fromItems = new List<FromItem>();

            Table_ref_listContext tableList = node.table_ref_list();
            Table_refContext[] tables = tableList.table_ref();

            foreach (Table_refContext table in tables)
            {
                FromItem fromItem = new FromItem();

                fromItem.TableName = this.ParseTableName(table);

                Join_clauseContext[] joins = table.join_clause();
                Pivot_clauseContext pivot = table.pivot_clause();
                Unpivot_clauseContext unpivot = table.unpivot_clause();

                if (joins != null && joins.Length > 0)
                {
                    foreach (Join_clauseContext join in joins)
                    {
                        JoinItem joinItem = new JoinItem();

                        string type = join.outer_join_type().GetText();

                        switch (type)
                        {
                            case nameof(PlSqlParser.LEFT):
                                joinItem.Type = JoinType.LEFT;
                                break;
                            case nameof(PlSqlParser.RIGHT):
                                joinItem.Type = JoinType.RIGHT;
                                break;
                            case nameof(PlSqlParser.FULL):
                                joinItem.Type = JoinType.FULL;
                                break;
                            case nameof(PlSqlParser.CROSS):
                                joinItem.Type = JoinType.CROSS;
                                break;
                        }

                        joinItem.TableName = this.ParseTableName(join.table_ref_aux());
                        joinItem.Condition = this.ParseCondition(join.join_on_part().FirstOrDefault()?.condition());

                        fromItem.JoinItems.Add(joinItem);
                    }
                }
                else if (pivot != null)
                {
                    JoinItem joinItem = new JoinItem() { Type = JoinType.PIVOT };
                    joinItem.PivotItem = this.ParsePivot(pivot);
                    fromItem.JoinItems.Add(joinItem);
                }
                else if (unpivot != null)
                {
                    JoinItem joinItem = new JoinItem() { Type = JoinType.UNPIVOT };
                    joinItem.UnPivotItem = this.ParseUnPivot(unpivot);
                    fromItem.JoinItems.Add(joinItem);
                }

                fromItems.Add(fromItem);
            }

            return fromItems;
        }

        public PivotItem ParsePivot(Pivot_clauseContext node)
        {
            PivotItem pivotItem = new PivotItem();

            Pivot_elementContext pm = node.pivot_element().FirstOrDefault();

            Aggregate_function_nameContext function = pm.aggregate_function_name();

            pivotItem.AggregationFunctionName = new TokenInfo(function.identifier());
            pivotItem.AggregatedColumnName = this.ParseToken(pm.expression(), TokenType.ColumnName);
            pivotItem.ColumnName = this.ParseColumnName(node.pivot_for_clause().column_name());
            pivotItem.Values = node.pivot_in_clause().pivot_in_clause_element().Select(item => new TokenInfo(item)).ToList();

            return pivotItem;
        }

        public UnPivotItem ParseUnPivot(Unpivot_clauseContext node)
        {
            UnPivotItem unpivotItem = new UnPivotItem();
            unpivotItem.ValueColumnName = this.ParseColumnName(node.column_name());
            unpivotItem.ForColumnName = this.ParseColumnName(node.pivot_for_clause().column_name());
            unpivotItem.InColumnNames = node.unpivot_in_clause().unpivot_in_elements().Select(item => this.ParseColumnName(item.column_name())).ToList();

            return unpivotItem;
        }

        public List<SetStatement> ParseSetStatement(Assignment_statementContext node)
        {
            List<SetStatement> statements = new List<SetStatement>();

            foreach (var child in node.children)
            {
                if (child is General_elementContext element)
                {
                    SetStatement statement = new SetStatement();

                    statement.Key = new TokenInfo(element);

                    statements.Add(statement);
                }
                else if (child is ExpressionContext exp)
                {
                    statements.Last().Value = this.ParseToken(exp);
                }
            }

            return statements;
        }

        public Statement ParseDeclareStatement(Declare_specContext node)
        {
            Statement statement = null;

            foreach (var child in node.children)
            {
                if (child is Variable_declarationContext variable)
                {
                    DeclareStatement declareStatement = new DeclareStatement();

                    declareStatement.Name = new TokenInfo(variable.identifier()) { Type = TokenType.VariableName };
                    declareStatement.DataType = new TokenInfo(variable.type_spec().GetText()) { Type = TokenType.DataType };

                    var expression = variable.default_value_part()?.expression();

                    if (expression != null)
                    {
                        declareStatement.DefaultValue = new TokenInfo(expression);
                    }

                    statement = declareStatement;
                }
                else if (child is Cursor_declarationContext cursor)
                {
                    DeclareCursorStatement declareCursorStatement = new DeclareCursorStatement();

                    declareCursorStatement.CursorName = new TokenInfo(cursor.identifier()) { Type = TokenType.CursorName };
                    declareCursorStatement.SelectStatement = this.ParseSelectStatement(cursor.select_statement());

                    statement = declareCursorStatement;
                }
            }

            return statement;
        }

        public IfStatement ParseIfStatement(If_statementContext node)
        {
            IfStatement statement = new IfStatement();

            IfStatementItem ifItem = new IfStatementItem() { Type = IfStatementType.IF };
            ifItem.Condition = new TokenInfo(node.condition()) { Type = TokenType.Condition };
            ifItem.Statements.AddRange(this.ParseSeqStatement(node.seq_of_statements()));
            statement.Items.Add(ifItem);

            foreach (Elsif_partContext elseif in node.elsif_part())
            {
                IfStatementItem elseIfItem = new IfStatementItem() { Type = IfStatementType.ELSEIF };
                elseIfItem.Condition = new TokenInfo(elseif.condition()) { Type = TokenType.Condition };
                elseIfItem.Statements.AddRange(this.ParseSeqStatement(elseif.seq_of_statements()));

                statement.Items.Add(elseIfItem);
            }

            Else_partContext @else = node.else_part();
            if (@else != null)
            {
                IfStatementItem elseItem = new IfStatementItem() { Type = IfStatementType.ELSE };
                elseItem.Statements.AddRange(this.ParseSeqStatement(@else.seq_of_statements()));

                statement.Items.Add(elseItem);
            }

            return statement;
        }

        public CaseStatement ParseCaseStatement(Case_statementContext node)
        {
            CaseStatement statement = new CaseStatement();

            Simple_case_statementContext simple = node.simple_case_statement();

            if (simple != null)
            {
                statement.VariableName = new TokenInfo(simple.expression()) { Type = TokenType.VariableName };

                Simple_case_when_partContext[] whens = simple.simple_case_when_part();

                foreach (Simple_case_when_partContext when in whens)
                {
                    IfStatementItem ifItem = new IfStatementItem() { Type = IfStatementType.IF };
                    ifItem.Condition = new TokenInfo(when.expression().First()) { Type = TokenType.Condition };
                    ifItem.Statements.AddRange(this.ParseSeqStatement(when.seq_of_statements()));
                    statement.Items.Add(ifItem);
                }

                Case_else_partContext @else = simple.case_else_part();

                if (@else != null)
                {
                    IfStatementItem elseItem = new IfStatementItem() { Type = IfStatementType.ELSE };
                    elseItem.Statements.AddRange(this.ParseSeqStatement(@else.seq_of_statements()));

                    statement.Items.Add(elseItem);
                }
            }

            return statement;
        }

        public LoopStatement ParseLoopStatement(Loop_statementContext node)
        {
            LoopStatement statement = new LoopStatement();

            int i = 0;

            foreach (var child in node.children)
            {
                if (child is TerminalNodeImpl terminalNode)
                {
                    if (i == 0)
                    {
                        int type = terminalNode.Symbol.Type;

                        if (type == PlSqlParser.FOR)
                        {
                            statement.Type = LoopType.FOR;
                        }
                        else if (type == PlSqlParser.WHILE)
                        {
                            statement.Type = LoopType.WHILE;
                        }
                        else if (type == PlSqlParser.LOOP)
                        {
                            statement.Type = LoopType.LOOP;
                        }
                    }
                }
                else if (child is Seq_of_statementsContext seq)
                {
                    statement.Statements.AddRange(this.ParseSeqStatement(seq));
                }
                else if (child is ConditionContext condition)
                {
                    statement.Condition = new TokenInfo(condition) { Type = TokenType.Condition };
                }
                else if (child is Cursor_loop_paramContext cursor)
                {
                    statement.Condition = new TokenInfo(cursor) { Type = TokenType.Condition };
                }

                i++;
            }

            return statement;
        }

        public Statement ParseReturnStatement(Return_statementContext node)
        {
            Statement statement = new ReturnStatement();

            var expressioin = node.expression();

            if (expressioin != null)
            {
                statement = new ReturnStatement() { Value = new TokenInfo(expressioin) };
            }
            else
            {
                statement = new LeaveStatement() { Content = new TokenInfo(node) };
            }

            return statement;
        }

        public TransactionStatement ParseTransactionStatement(Transaction_control_statementsContext node)
        {
            TransactionStatement statement = new TransactionStatement();
            statement.Content = new TokenInfo(node);

            if (node.set_transaction_command() != null)
            {
                statement.CommandType = TransactionCommandType.SET;
            }
            else if (node.commit_statement() != null)
            {
                statement.CommandType = TransactionCommandType.COMMIT;
            }
            else if (node.rollback_statement() != null)
            {
                statement.CommandType = TransactionCommandType.ROLLBACK;
            }

            return statement;
        }

        public override TableName ParseTableName(ParserRuleContext node, bool strict = false)
        {
            TableName tableName = null;

            Action<Table_aliasContext> setAlias = (alias) =>
            {
                if (tableName != null && alias != null)
                {
                    tableName.Alias = new TokenInfo(alias);
                }
            };

            if (node != null)
            {
                if (node is Tableview_nameContext tv)
                {
                    tableName = new TableName(tv);
                }
                else if (node is Table_ref_aux_internal_oneContext traio)
                {
                    tableName = new TableName(traio);
                }
                else if (node is General_table_refContext gtr)
                {
                    tableName = new TableName(gtr);

                    tableName.Name = new TokenInfo(gtr.dml_table_expression_clause().tableview_name()) { Type = TokenType.TableName };

                    setAlias(gtr.table_alias());
                }
                else if (node is Table_ref_auxContext tra)
                {
                    tableName = new TableName(tra);

                    tableName.Name = new TokenInfo(tra.table_ref_aux_internal()) { Type = TokenType.TableName };

                    setAlias(tra.table_alias());
                }
                else if (node is Table_ref_listContext trl)
                {
                    return this.ParseTableName(trl.table_ref().FirstOrDefault());
                }
                else if (node is Table_refContext tr)
                {
                    return this.ParseTableName(tr.table_ref_aux());
                }

                if (!strict && tableName == null)
                {
                    tableName = new TableName(node);
                }
            }

            return tableName;
        }

        public override ColumnName ParseColumnName(ParserRuleContext node, bool strict = false)
        {
            ColumnName columnName = null;

            if (node != null)
            {
                if (node is Column_nameContext cn)
                {
                    Id_expressionContext[] ids = cn.id_expression();
                    IdentifierContext id = cn.identifier();
                    if (ids != null && ids.Length > 0)
                    {
                        columnName = new ColumnName(ids[0]);
                        columnName.Name = new TokenInfo(ids[0]);
                    }
                    else if (id != null)
                    {
                        columnName = new ColumnName(id);
                        columnName.Name = new ColumnName(id);
                    }
                }
                else if (node is Select_list_elementsContext ele)
                {
                    columnName = new ColumnName(ele);

                    Tableview_nameContext tableName = ele.tableview_name();
                    ExpressionContext expression = ele.expression();
                    Column_aliasContext alias = ele.column_alias();

                    if (tableName != null)
                    {
                        columnName.TableName = new TokenInfo(tableName);
                    }

                    if (expression != null)
                    {
                        columnName.Name = new TokenInfo(expression);
                    }

                    if (alias != null)
                    {
                        columnName.Alias = new TokenInfo(alias.identifier());
                    }
                }
                else if (node is General_element_partContext gele)
                {
                    if (this.IsChildOfType<Select_list_elementsContext>(gele))
                    {
                        Id_expressionContext[] ids = gele.id_expression();

                        if (ids != null && ids.Length > 0)
                        {
                            if (ids.Length > 1)
                            {
                                columnName = new ColumnName(ids[1]);
                            }
                            else
                            {
                                columnName = new ColumnName(ids[0]);
                            }
                        }
                    }
                }

                if (!strict && columnName == null)
                {
                    columnName = new ColumnName(node);
                }
            }

            return columnName;
        }

        private TokenInfo ParseCondition(ParserRuleContext node)
        {
            if (node != null)
            {
                if (node is ConditionContext ||
                    node is Where_clauseContext ||
                    node is ExpressionContext)
                {
                    return this.ParseToken(node, TokenType.Condition);
                }
            }

            return null;
        }

        public override bool IsFunction(IParseTree node)
        {
            if (node is Standard_functionContext)
            {
                return true;
            }
            else if (node is General_element_partContext && (node as General_element_partContext).children.Any(item => item is Function_argumentContext))
            {
                return true;
            }
            return false;
        }

        public override List<TokenInfo> GetTableNameTokens(IParseTree node)
        {
            List<TokenInfo> tokens = new List<TokenInfo>();

            TableName tableName = this.ParseTableName(node as ParserRuleContext, true);

            if (tableName != null)
            {
                tokens.Add(tableName);
            }

            return tokens;
        }

        public override List<TokenInfo> GetColumnNameTokens(IParseTree node)
        {
            List<TokenInfo> tokens = new List<TokenInfo>();

            ColumnName columnName = this.ParseColumnName(node as ParserRuleContext, true);

            if (columnName != null)
            {
                tokens.Add(columnName);
            }

            return tokens;
        }

        private bool IsChildOfType<T>(RuleContext node)
        {
            if (node == null || node.Parent == null)
            {
                return false;
            }

            if (node.Parent != null && node.Parent.GetType() == typeof(T))
            {
                return true;
            }
            else
            {
                return this.IsChildOfType<T>(node.Parent as RuleContext);
            }
        }

        public override List<TokenInfo> GetRoutineNameTokens(IParseTree node)
        {
            List<TokenInfo> tokens = new List<TokenInfo>();

            ParserRuleContext routineName = null;

            if (node is General_element_partContext gep && (node as General_element_partContext).children.Any(item => item is Function_argumentContext))
            {
                routineName = gep.id_expression().LastOrDefault();
            }

            if (routineName != null)
            {
                tokens.Add(new TokenInfo(routineName) { Type = TokenType.RoutineName });
            }

            return tokens;
        }
    }
}
