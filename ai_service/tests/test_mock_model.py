import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
from rules import mock_model  # noqa: E402


def f(key, name, ftype, synonyms=(), required=False):
    return {"key": key, "displayName": name, "type": ftype, "synonyms": list(synonyms), "isRequired": required}


CATALOG = [
    {
        "key": "Employee", "displayName": "עובד", "pluralName": "עובדים", "synonyms": ["עובד", "עובדים"],
        "fields": [
            f("Email", 'דוא"ל', "string", ["מייל"], True),
            f("Age", "גיל", "number", ["גילם"]),
            f("HireDate", "תאריך תחילת העסקה", "date", ["תחילת העסקה"]),
            f("EndDate", "תאריך סיום העסקה", "date", ["סיום העסקה"]),
            f("Salary", "שכר", "number", ["משכורת", "משכורתם", "שכרם"], True),
            f("IsActive", "פעיל", "boolean", ["פעילים"]),
            f("DepartmentId", "מחלקה משויכת", "number", [], True),
        ],
        "relations": [
            {"key": "Department", "displayName": "מחלקה", "targetEntity": "Department", "synonyms": ["מחלקה", "למחלקה"]},
            {"key": "JobTitle", "displayName": "תפקיד", "targetEntity": "JobTitle", "synonyms": []},
        ],
    },
    {
        "key": "Department", "displayName": "מחלקה", "pluralName": "מחלקות", "synonyms": ["מחלקה"],
        "fields": [f("IsActive", "פעילה", "boolean", ["פעיל", "פעילה", "פעילים"])],
        "relations": [],
    },
    {
        "key": "JobTitle", "displayName": "תפקיד", "pluralName": "תפקידים", "synonyms": [],
        "fields": [f("MaxSalary", "שכר מקסימום לתפקיד", "number", ["תקרת שכר"])],
        "relations": [],
    },
]


def conditions(result):
    return result["definition"]["root"]["items"]


class MockModelTests(unittest.TestCase):
    def suggest(self, prompt):
        return mock_model.suggest_control({"prompt": prompt, "catalog": CATALOG})

    def test_example_from_brief(self):
        r = self.suggest('מצא עובדים פעילים שגילם נמוך מ־18 ומשכורתם גבוהה מ־15,000 ש"ח.')
        self.assertEqual(r["definition"]["rootEntity"], "Employee")
        ops = {(c["field"]["field"], c["operator"], c.get("value")) for c in conditions(r)}
        self.assertEqual(ops, {("IsActive", "eq", True), ("Age", "lt", 18), ("Salary", "gt", 15000)})

    def test_ascii_hyphen_prefix_is_not_a_minus_sign(self):
        r = self.suggest("עובדים שגילם נמוך מ-18")
        self.assertEqual(r["definition"]["root"]["items"][0]["value"], 18)

    def test_less_or_equal_phrase_maps_to_lte(self):
        r = self.suggest("עובדים שגילם נמוך או שווה 18")
        self.assertEqual(conditions(r)[0]["operator"], "lte")

    def test_type_is_first_key_for_polymorphic_json(self):
        r = self.suggest("עובדים שגילם נמוך מ 18")
        self.assertEqual(next(iter(r["definition"]["root"])), "type")
        self.assertEqual(next(iter(conditions(r)[0])), "type")

    def test_end_date_before_start_date_is_field_comparison(self):
        r = self.suggest("איתור עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם")
        self.assertEqual(len(conditions(r)), 1)
        c = conditions(r)[0]
        self.assertEqual((c["field"]["field"], c["operator"], c["compareField"]["field"]), ("EndDate", "lt", "HireDate"))

    def test_active_employee_in_inactive_department_uses_relation_path(self):
        r = self.suggest("איתור עובדים פעילים המשויכים למחלקה שאינה פעילה")
        found = {(tuple(c["field"]["path"]), c["field"]["field"], c["value"]) for c in conditions(r)}
        self.assertEqual(found, {((), "IsActive", True), (("Department",), "IsActive", False)})

    def test_missing_data_without_fields_uses_required_fields(self):
        r = self.suggest("איתור עובדים שבהם חסרים נתונים")
        group = conditions(r)[0]
        self.assertEqual(group["operator"], "or")
        self.assertEqual({c["field"]["field"] for c in group["items"]}, {"Email", "Salary", "DepartmentId"})

    def test_unknown_text_returns_warning_and_low_confidence(self):
        r = self.suggest("שלום עולם")
        self.assertEqual(conditions(r), [])
        self.assertTrue(r["warnings"])
        self.assertLess(r["confidence"], 0.5)

    def test_proactive_suggestions_are_generic_over_catalog(self):
        names = [s["name"] for s in mock_model.suggest_proactive({"catalog": CATALOG})["suggestions"]]
        self.assertTrue(any("חסרים נתוני חובה" in n for n in names))
        self.assertTrue(any("מוקדם" in n for n in names))
        self.assertTrue(any("שאינו פעיל" in n for n in names))
        self.assertTrue(any("חורג" in n for n in names))


if __name__ == "__main__":
    unittest.main()
