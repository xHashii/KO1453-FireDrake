-- Name-only startup repair uses a guarded allowlist and transaction.
-- Never overwrite a nonmatching custom name or change actor stats/placement.
CREATE TABLE IF NOT EXISTS KO_ACTOR_NAME_BACKUP (
 TableName varchar(40) NOT NULL,
 Sid int NOT NULL,
 Model int NOT NULL,
 OldName varchar(255) NULL,
 NewName varchar(255) NOT NULL,
 AppliedUtc datetime(6) NOT NULL,
 PRIMARY KEY (TableName,Sid,Model)
) ENGINE=InnoDB;
