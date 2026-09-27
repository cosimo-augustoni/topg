-- topg quiz import
-- Quiz:      Pub Quiz – O'Brien's "Night"
-- Generated: 2026-09-27 12:00:00 UTC
-- Boards: 2, questions: 4, replace existing: no
-- Images are expected at https://cdn.example.com/quiz/pub-quiz/
DO $topg$
DECLARE
    template_id bigint;
    board_id bigint;
BEGIN
    INSERT INTO "Templates" ("Name") VALUES ('Pub Quiz – O''Brien''s "Night"') RETURNING "Id" INTO template_id;

    -- Board 1
    INSERT INTO "Boards" ("TemplateId", "Order") VALUES (template_id, 0) RETURNING "Id" INTO board_id;
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "TextQuestion_QuestionText", "CorrectAnswer") VALUES
        (board_id, 0, 0, 100, 'Zürich', 'It''s 100 %?', 'Yes'),
        (board_id, 0, 0, 200, 'Zürich', 'Line 1
Line 2', 'C:\path');
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "QuestionText", "QuestionImageUri", "AnswerText", "AnswerImageUri", "ImageSize") VALUES
        (board_id, 2, 1, 100, 'Animals', 'Which flag?', 'https://cdn.example.com/quiz/pub-quiz/807d0fbcae7c4b20.png', 'Switzerland', 'https://cdn.example.com/quiz/pub-quiz/0db52f4076c08251.png', 2);

    -- Board 2
    INSERT INTO "Boards" ("TemplateId", "Order") VALUES (template_id, 1) RETURNING "Id" INTO board_id;
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "QuestionText", "QuestionImageUri", "AnswerText", "AnswerImageUri", "ImageSize") VALUES
        (board_id, 2, 0, 300, 'Second', 'No answer image', 'https://cdn.example.com/quiz/pub-quiz/807d0fbcae7c4b20.png', '', '', 1);
END
$topg$;
