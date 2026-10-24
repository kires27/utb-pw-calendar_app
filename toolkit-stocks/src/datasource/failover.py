import traceback

from lib.module.printf import printf, level as lv


def nonempty(result) -> bool:
	'''Default validation: result exists and is truthy (non-empty list/dict).'''
	return bool(result)


def price_rows_valid(rows) -> bool:
	'''Price history rows: non-empty, every row has date + close.'''
	if not rows:
		return False
	return all(r.get("date") and r.get("close") is not None for r in rows)


class FailoverSource:
	'''
	Per-symbol failover wrapper: try primary, validate, else fallback.
	Same method surface as the underlying sources where both can serve.

	Contract (tests/contract/test_failover_contract.py): get(symbol) tries
	primary then fallback, returning (result, source_name) or (None, None).
	Real sources have no .get, so get() duck-types any callable member.

	Prices are all-or-nothing per symbol's full history: sources may carry
	different event completeness (dividends/splits), and adj_close is
	recomputed over the whole series - splicing histories mid-way would
	corrupt every row before the splice point.
	'''

	def __init__(self, primary, fallback, validate=nonempty, label: str = "failover"):
		self.primary = primary
		self.fallback = fallback
		self.validate = validate
		self.label = label

	def get(self, symbol):
		'''Contract method: works with any source exposing .get(symbol).'''
		for name, source in (("primary", self.primary), ("fallback", self.fallback)):
			try:
				result = source.get(symbol)
				if self.validate(result):
					return result, name
			except Exception:
				pass
		return None, None

	def _per_symbol(self, symbols: list[str], primary_fn, fallback_fn, label: str) -> dict:
		'''
		Primary per symbol; on raise/invalid result, retry via fallback_fn.
		Returns {symbol: result} only for symbols served by either source.
		'''
		out: dict = {}
		for symbol in symbols:
			try:
				result = primary_fn(symbol)
				if self.validate(result):
					out[symbol] = result
					continue
			except Exception as err:
				tb = traceback.extract_tb(err.__traceback__)[-1]
				printf(f"{symbol}: primary {label} failed ({err}) (File '{tb.filename}', line {tb.lineno})", lv.WARNING)
			try:
				result = fallback_fn(symbol)
				if self.validate(result):
					out[symbol] = result
					printf(f"{symbol}: {label} served by fallback", lv.INFO)
					continue
			except Exception as err:
				tb = traceback.extract_tb(err.__traceback__)[-1]
				printf(f"{symbol}: fallback {label} failed ({err}) (File '{tb.filename}', line {tb.lineno})", lv.WARNING)
			printf(f"{symbol}: {label} unavailable from both sources", lv.WARNING)
		if len(out) < len(symbols):
			printf(f"{label}: {len(out)}/{len(symbols)} symbols served", lv.INFO)
		return out

	# --- market data: all-or-nothing per symbol's full history ---

	def prices(self, symbols: list[str]) -> dict[str, list[dict]]:
		'''symbol -> full price history rows (primary) or yfinance history_payload rows (fallback).'''
		def fallback(symbol: str):
			payload = self.fallback.history_payload(symbol)
			return payload["price_rows"] if payload else None

		return self._per_symbol(symbols, lambda s: self.primary.prices([s]).get(s), fallback, "prices")

	def dividends(self, symbols: list[str]) -> dict[str, list[dict]]:
		'''symbol -> [{date, amount}] from primary events or yfinance history payload.'''
		def fallback(symbol: str):
			payload = self.fallback.history_payload(symbol)
			return payload["div_events"] if payload else None

		return self._per_symbol(symbols, lambda s: self.primary.dividends([s]).get(s), fallback, "dividends")

	def splits(self, symbols: list[str]) -> dict[str, list[dict]]:
		'''symbol -> [{date, factor}] from primary events or yfinance history payload.'''
		def fallback(symbol: str):
			payload = self.fallback.history_payload(symbol)
			return payload["split_events"] if payload else None

		return self._per_symbol(symbols, lambda s: self.primary.splits([s]).get(s), fallback, "splits")

	def shares_outstanding(self, symbols: list[str]) -> dict[str, float | None]:
		'''symbol -> latest shares; fallback reads info().sharesOutstanding.'''
		def fallback(symbol: str):
			info = self.fallback.info(symbol)
			return info.get("sharesOutstanding") if info else None

		return self._per_symbol(symbols, lambda s: self.primary.shares_outstanding([s]).get(s), fallback, "shares")

	# --- DefeatBeta-only calls the pipeline makes: delegate to primary ---

	def us_members(self, symbols: list[str]) -> set[str]:
		return self.primary.us_members(symbols)

	def statements(self, symbols: list[str], finance_type: str, period_type: str = "annual") -> dict[str, list[dict]]:
		return self.primary.statements(symbols, finance_type, period_type)

	# --- profiles: fail over freely per call ---

	def profiles(self, symbols: list[str]) -> dict[str, dict]:
		def fallback(symbol: str):
			info = self.fallback.info(symbol)
			return info or None

		return self._per_symbol(symbols, lambda s: self.primary.profiles([s]).get(s), fallback, "profiles")
