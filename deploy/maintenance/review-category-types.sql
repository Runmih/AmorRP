-- READ-ONLY review aid for databases that already applied the old M4 migration.
-- Results include legitimate defaults and owner choices: they are candidates for
-- owner review, NOT proof of a bad policy or instructions to remove item types.
-- Do not reset migration history or blindly UPDATE these rows.
BEGIN TRANSACTION READ ONLY;

SELECT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20261014230954_TradingBeta'
) AS "TradingBetaAlreadyApplied";

SELECT g."Id" AS "GroupId", g."Name" AS "GroupName",
       c."Id" AS "CategoryId", c."Name" AS "CategoryName",
       c."AllowedTypeIds", c."Retired", c."Version"
FROM categories c
JOIN groups g ON g."Id" = c."GroupId"
WHERE 'letter' = ANY(c."AllowedTypeIds")
  AND ('book' = ANY(c."AllowedTypeIds") OR 'note' = ANY(c."AllowedTypeIds"))
ORDER BY g."Name", g."Id", c."Name", c."Id";

COMMIT;
