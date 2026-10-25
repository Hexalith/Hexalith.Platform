"""Closed shipped qualification-driver inventory; installation signatures cannot fill missing source drivers."""
from pathlib import Path
from cases import plan
import json

PATH = Path(__file__).with_name('case-driver-inventory.json')


def load_inventory():
    value = json.loads(PATH.read_text())
    rows = value.get('cases', [])
    expected = [case.id for case in plan()]
    if value.get('schema') != 'EXT-SECRETS-1.closed-case-drivers.v1' or not isinstance(rows,list) or [row.get('caseId') for row in rows] != expected:
        raise ValueError('ExactCompleteShippedDriverInventory')
    return value


def blockers(case_ids=None):
    selected = set(case_ids) if case_ids is not None else {case.id for case in plan()}
    result = []
    for row in load_inventory()['cases']:
        if row['caseId'] not in selected: continue
        if row.get('completeQualificationDriver') is not True:
            result.extend('CaseDriver.' + row['caseId'] + '.' + needed for needed in [*row['requiredSourceImplementations'],*row['requiredExternalInputs']])
        else:
            driver = row.get('qualificationDriver')
            if not isinstance(driver,str) or not (Path(__file__).resolve().parents[2]/driver).is_file():
                result.append('CaseDriver.'+row['caseId']+'.ConcreteShippedExecutable')
    return sorted(set(result))
