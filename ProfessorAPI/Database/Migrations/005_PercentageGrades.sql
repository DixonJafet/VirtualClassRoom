-- Apply once after 004. Values and grades are course percentage points.
START TRANSACTION;

UPDATE grades g
JOIN evaluations e ON e.evaluation_id = g.evaluation_id
SET g.grade = ROUND(g.grade * e.course_weight / e.value, 2)
WHERE g.grade IS NOT NULL AND e.value > 0;

UPDATE evaluations
SET value = course_weight;

COMMIT;
