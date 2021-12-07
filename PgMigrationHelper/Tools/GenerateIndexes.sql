SELECT
        'CREATE '+(CASE WHEN i.is_unique=1 THEN 'UNIQUE' ELSE '' END)+' INDEX "'+i.[name]+'" ON "'+schema_name(t.schema_id)+'"."'+t.[name]+'" ('+STRING_AGG('"'+col.[name]+'"', ' ASC, ')+' ASC);' AS [Output]
FROM sys.objects t
         JOIN sys.indexes i ON t.object_id = i.object_id
         JOIN sys.index_columns ic ON ic.index_id = i.index_id AND ic.object_id = t.object_id
         JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
WHERE t.is_ms_shipped <> 1
  AND i.index_id > 0
  AND schema_name(t.schema_id) + '.' + t.[name] NOT IN ({0})
  AND i.is_primary_key = 0
GROUP BY i.is_unique, i.[name], t.[name], t.schema_id
ORDER BY i.[name]
