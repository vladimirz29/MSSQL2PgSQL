DECLARE @t1 TABLE(TableSchema NVARCHAR(MAX), TableName NVARCHAR(MAX), ColumnName NVARCHAR(MAX), OrdinalPosition INT, IsNullable BIT)

INSERT INTO @t1 (TableSchema, TableName, ColumnName, OrdinalPosition, IsNullable)
SELECT t.TABLE_SCHEMA, t.TABLE_NAME, c.COLUMN_NAME, c.ORDINAL_POSITION, CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END
FROM INFORMATION_SCHEMA.TABLES t
         JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_CATALOG = c.TABLE_CATALOG AND t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
WHERE t.TABLE_SCHEMA + '.' + t.TABLE_NAME NOT IN ({2})
ORDER BY c.ORDINAL_POSITION


SELECT 'bcp "SELECT '+ STRING_AGG('CASE WHEN CAST(['+t.ColumnName+'] AS NVARCHAR(MAX)) = ''0'' THEN ''0'' WHEN LEN(CAST(['+t.ColumnName+'] AS NVARCHAR(MAX))) = 0 THEN ''""""'' WHEN ['+t.ColumnName+'] IS NOT NULL THEN (REPLACE(REPLACE([' + t.ColumnName + '],CHAR(10),''\r''),CHAR(13),''\n'')) END', ',') WITHIN GROUP (ORDER BY t.OrdinalPosition) +' FROM ['+ t.TableSchema +'].['+ t.TableName +']" queryout "{0}'+ t.TableSchema +'.'+ t.TableName +'.csv" -t "|" -c -S . -d {1} -T -k' AS [Output]
FROM @t1 t
GROUP BY t.TableSchema, t.TableName
ORDER BY t.TableName
