-- Deploy before the API version that calls this procedure.
-- Existing classroom and schedule procedures are intentionally unchanged.
DELIMITER $$

CREATE PROCEDURE GetClassroomsWithSchedules(
    IN p_professor_id VARCHAR(255),
    IN p_classroom_code VARCHAR(70)
)
BEGIN
    SELECT c.classroomCode, c.courseName, c.about
    FROM classrooms c
    WHERE c.professor_id = p_professor_id
      AND (p_classroom_code IS NULL OR c.classroomCode = p_classroom_code)
    ORDER BY c.classroom_id DESC;

    SELECT c.classroomCode, sh.day,
           CONCAT(DATE_FORMAT(sh.from_time, '%H:%i'), '-', DATE_FORMAT(sh.to_time, '%H:%i')) AS time
    FROM classrooms c
    INNER JOIN schedules sh ON sh.classroom_id = c.classroom_id
    WHERE c.professor_id = p_professor_id
      AND (p_classroom_code IS NULL OR c.classroomCode = p_classroom_code)
    ORDER BY c.classroom_id DESC, sh.schedule_id;
END$$

DELIMITER ;
