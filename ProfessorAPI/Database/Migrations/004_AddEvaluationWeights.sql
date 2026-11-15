-- Keep value as the legacy maximum score until every client uses course_weight.
-- Apply once, after backing up the database.
ALTER TABLE evaluations ADD COLUMN course_weight DECIMAL(5,2) NULL;

UPDATE evaluations e
JOIN (
    SELECT evaluation_id,
           CASE WHEN total_value > 100
                THEN (base_cents + (rank_in_class <= 10000 - total_base)) / 100
                ELSE value END AS weight
    FROM (
        SELECT evaluation_id, value, total_value, base_cents,
               SUM(base_cents) OVER (PARTITION BY classroom_id) AS total_base,
               ROW_NUMBER() OVER (
                   PARTITION BY classroom_id
                   ORDER BY raw_cents - base_cents DESC, evaluation_id
               ) AS rank_in_class
        FROM (
            SELECT evaluation_id, classroom_id, value, total_value,
                   value * 10000 / total_value AS raw_cents,
                   FLOOR(value * 10000 / total_value) AS base_cents
            FROM (
                SELECT evaluation_id, classroom_id, value,
                       SUM(value) OVER (PARTITION BY classroom_id) AS total_value
                FROM evaluations
            ) source_values
        ) calculated
    ) ranked
) weights ON weights.evaluation_id = e.evaluation_id
SET e.course_weight = weights.weight;
