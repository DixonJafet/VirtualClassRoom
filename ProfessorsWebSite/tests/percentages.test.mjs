import { test } from 'node:test';
import assert from 'node:assert/strict';
import { courseTotal, validGrade } from '../src/utils/percentages.ts';

test('grades are course percentages, not percentages of the evaluation value', () => {
  assert.equal(courseTotal({ Name: 'Student', Quiz: 5, Exam: 20, total: 99 }), 25);
  assert.equal(courseTotal({ Name: 'Student', Quiz: '5.25' }), 5.25);
});

test('a grade is between zero and the evaluation value', () => {
  assert.equal(validGrade(5, 10), true);
  assert.equal(validGrade(0, 0), true);
  assert.equal(validGrade(10, 10), true);
  assert.equal(validGrade(-1, 10), false);
  assert.equal(validGrade(10.01, 10), false);
  assert.equal(validGrade(5, 101), false);
  assert.equal(validGrade(Number.NaN, 10), false);
});
