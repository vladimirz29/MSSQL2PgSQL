DECLARE @EqualTypes TABLE(MsSqlType NVARCHAR(MAX), PostgreSqlType NVARCHAR(MAX))

INSERT INTO @EqualTypes (MsSqlType, PostgreSqlType)
VALUES
('bigint', 'bigint'),
('binary', 'bytea'),
('bit', 'boolean'),
('char', 'char'),
('varchar', 'text'),
('nvarchar', 'text'),
('text', 'text'),
('ntext', 'text'),
('double precision', 'double precision'),
('float', 'double precision'),
('int', 'integer'),
('integer', 'integer'),
('numeric', 'numeric'),
('date', 'date'),
('datetime', 'timestamp(3)'),
('datetime2', 'timestamp'),
('tinyint', 'smallint'),
('uniqueidentifier', 'uuid'),
('smallmoney', 'money'),
('image', 'bytea'),
('smallint', 'smallint'),
('money', 'numeric(19,4)')
