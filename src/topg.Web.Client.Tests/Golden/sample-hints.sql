-- topg quiz import
-- Quiz:      Pub Quiz – O'Brien's "Night"
-- Generated: 2026-09-27 12:00:00 UTC
-- Boards: 2, questions: 7, replace existing: yes
-- Images are expected at https://cdn.example.com/quiz/pub-quiz/
DO $topg$
DECLARE
    template_id bigint;
    board_id bigint;
    question_id bigint;
BEGIN
    -- Replace existing templates with this name. Questions don't cascade from boards, so delete them first.
    DELETE FROM "Questions" WHERE "BoardId" IN (
        SELECT b."Id" FROM "Boards" b JOIN "Templates" t ON t."Id" = b."TemplateId" WHERE t."Name" = 'Pub Quiz – O''Brien''s "Night"');
    DELETE FROM "Templates" WHERE "Name" = 'Pub Quiz – O''Brien''s "Night"';

    INSERT INTO "Templates" ("Name") VALUES ('Pub Quiz – O''Brien''s "Night"') RETURNING "Id" INTO template_id;

    -- Board 1
    INSERT INTO "Boards" ("TemplateId", "Order") VALUES (template_id, 0) RETURNING "Id" INTO board_id;
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "TextQuestion_QuestionText", "CorrectAnswer") VALUES
        (board_id, 0, 0, 400, 'Cities', 'No hints here', 'Plain'),
        (board_id, 0, 0, 100, 'Zürich', 'It''s 100 %?', 'Yes'),
        (board_id, 0, 0, 200, 'Zürich', 'Line 1
Line 2', 'C:\path');
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "TextQuestion_QuestionText", "CorrectAnswer", "HintType") VALUES
        (board_id, 0, 0, 300, 'Cities', 'Which city?', 'Basel', 0) RETURNING "Id" INTO question_id;
    INSERT INTO "QuestionHints" ("QuestionId", "Order", "Text", "ImageUri") VALUES
        (question_id, 0, 'It''s on the "Rhine".', ''),
        (question_id, 1, 'Line 1
Line 2', ''),
        (question_id, 2, 'C:\path', '');
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "QuestionText", "QuestionImageUri", "AnswerText", "AnswerImageUri", "ImageSize", "StartPixelated") VALUES
        (board_id, 2, 1, 100, 'Animals', 'Which flag?', 'https://cdn.example.com/quiz/pub-quiz/807d0fbcae7c4b20.png', 'Switzerland', 'https://cdn.example.com/quiz/pub-quiz/0db52f4076c08251.png', 2, TRUE);

    -- Board 2
    INSERT INTO "Boards" ("TemplateId", "Order") VALUES (template_id, 1) RETURNING "Id" INTO board_id;
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "TextQuestion_QuestionText", "CorrectAnswer", "HintType") VALUES
        (board_id, 0, 0, 500, 'Flags', 'Which country?', 'Switzerland', 1) RETURNING "Id" INTO question_id;
    INSERT INTO "QuestionHints" ("QuestionId", "Order", "Text", "ImageUri") VALUES
        (question_id, 0, 'Red', 'https://cdn.example.com/quiz/pub-quiz/b1f51a511f1da0cd.png'),
        (question_id, 1, '', 'https://cdn.example.com/quiz/pub-quiz/018fa96a44715c90.png'),
        (question_id, 2, 'O''Brien''s cross', 'https://cdn.example.com/quiz/pub-quiz/b3986952b145da5f.png');
    INSERT INTO "Questions" ("BoardId", "QuestionType", "AnswerType", "Points", "Category", "QuestionText", "QuestionImageUri", "AnswerText", "AnswerImageUri", "ImageSize", "StartPixelated") VALUES
        (board_id, 2, 0, 300, 'Second', 'No answer image', 'https://cdn.example.com/quiz/pub-quiz/807d0fbcae7c4b20.png', '', '', 1, FALSE);
END
$topg$;
