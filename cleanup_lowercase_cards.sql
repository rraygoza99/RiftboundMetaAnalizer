-- Preview counts
SELECT 'DeckCards to delete' AS action, COUNT(*) AS count
FROM "DeckCards" dc
WHERE dc."CardId" ~ '^[a-z]';

SELECT 'Cards to delete' AS action, COUNT(*) AS count
FROM "Cards"
WHERE "Id" ~ '^[a-z]';

-- Step 1: Delete DeckCards referencing lowercase card IDs
DELETE FROM "DeckCards"
WHERE "CardId" ~ '^[a-z]';

-- Step 2: Delete the lowercase cards themselves
DELETE FROM "Cards"
WHERE "Id" ~ '^[a-z]';

-- Verify
SELECT 'Remaining cards' AS status, COUNT(*) AS count FROM "Cards";
