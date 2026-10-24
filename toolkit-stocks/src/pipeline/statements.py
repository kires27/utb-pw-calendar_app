from lib.transforms import build_statement_rows
from lib.transforms.statements import group_statement_records
from lib.module.printf import printf, level as lv
from lib.supabase.sql_functions import insert_data_statement
from .context import valid_us, valid_intl


def run_statements(ctx, finance_type: str, db_table: str, null_tolerance: float):
	'''One statement kind: balance_sheet | cash_flow | income_statement. Annual only.'''
	yfs = ctx.yfs
	period_type = "annual"

	grouped_us = {
		symbol: group_statement_records(records)
		for symbol, records in ctx.ds.statements(valid_us(ctx), finance_type, period_type).items()
	}

	def yf_records(sym):
		return yfs.statements_payload(sym, finance_type, period_type)

	grouped_intl = yfs.map_symbols(yf_records, valid_intl(ctx), finance_type) if ctx.intl_symbols else {}

	for source_grouped in (grouped_us, grouped_intl):
		for symbol, records in source_grouped.items():
			printf(f"---- {symbol} {finance_type} ----", lv.INFO)
			insert_data_statement(
				build_statement_rows(symbol, ctx.stock_ids[symbol], records, period_type, finance_type),
				db_table,
				null_tolerance=null_tolerance
			)
