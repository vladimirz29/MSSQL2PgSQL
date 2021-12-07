SELECT
        'ALTER TABLE "'+schema_name(tab.schema_id)+'"."'+tab.[name]+'" ADD CONSTRAINT "PK_'+tab.[name]+'" PRIMARY KEY ('+STRING_AGG('"'+col.[name]+'"', ', ')+');' AS [Output]
from sys.tables tab
         inner join sys.indexes pk
                    on tab.object_id = pk.object_id
                        and pk.is_primary_key = 1
         JOIN sys.index_columns ic ON ic.index_id = pk.index_id AND ic.object_id = tab.object_id
         JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
WHERE schema_name(tab.schema_id) + '.' + tab.[name] NOT IN ({0})
  AND col.system_type_id = 56
GROUP BY tab.schema_id, tab.[name], pk.[name]
HAVING COUNT(col.[name])>1
order by schema_name(tab.schema_id),
         pk.[name]
