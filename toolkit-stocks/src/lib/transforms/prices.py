import traceback

from lib.module.printf import printf, level as lv


def _clean(value) -> float | None:
	try:
		if value is None:
			return None
		f = float(value)
		return None if f != f else f	# NaN check without pandas
	except (TypeError, ValueError):
		return None


def compute_adj_close(rows_desc: list[dict], div_events: list[dict], split_events: list[dict]) -> dict[str, float]:
	'''
	Recompute dividend+split adjusted close over the full price history.

	rows_desc: price rows sorted newest-first: {date, close, ...}
	div_events: [{date, amount}], split_events: [{date, factor}]

	Walks backwards from the newest close (adj = close). Events apply to all
	prices strictly before the event date - even when the event date itself
	has no price row (holiday). Dividend scaling references the close of the
	nearest price row after the event (last seen while walking backwards).
	Returns {date: adj_close}.
	'''
	events: list[tuple[str, str, object]] = []
	for d in div_events:
		events.append((str(d["date"])[:10], "div", d["amount"]))
	for d in split_events:
		events.append((str(d["date"])[:10], "split", d["factor"]))
	events.sort(key=lambda e: e[0], reverse=True)

	factor = 1.0
	last_seen_close: float | None = None
	out: dict[str, float] = {}
	event_index = 0

	for row in rows_desc:
		date = str(row["date"])[:10]
		close = _clean(row["close"])
		if close is None:
			continue

		# Apply every event newer than this price row (walking backwards)
		while event_index < len(events) and events[event_index][0] > date:
			_, kind, value = events[event_index]
			if kind == "div":
				div = _clean(value)
				if div is not None and last_seen_close:
					factor *= 1.0 - div / last_seen_close
			else:
				split = _clean(value)
				if split is not None and split != 0:
					factor *= 1.0 / split
			event_index += 1

		out[date] = close * factor
		last_seen_close = close

	return out


def build_price_rows(symbol: str, stock_id: int, rows_desc: list[dict], div_events: list[dict], split_events: list[dict], latest_date) -> list[dict]:
	'''
	Transform full price history into DB rows, incremental: only dates on/after
	latest_date are emitted, but adjustment uses the full history so past
	dividends/splits stay priced in.
	'''
	try:
		if stock_id is None:
			raise TypeError("stock_id is None")

		adj_close_by_date = compute_adj_close(rows_desc, div_events, split_events)

		rows = []
		for row in rows_desc:
			d = str(row["date"])[:10]
			if latest_date is not None and d < str(latest_date):
				continue

			close = _clean(row["close"])
			if close is None:
				continue

			rows.append({
				"stock_id": stock_id,
				"date": d,
				"open": _clean(row.get("open")),
				"high": _clean(row.get("high")),
				"low": _clean(row.get("low")),
				"close": close,
				"volume": _clean(row.get("volume")),
				"adj_close": adj_close_by_date.get(d),
			})

		return rows
	except Exception as err:
		tb = traceback.extract_tb(err.__traceback__)[-1]
		printf(f"{symbol}: {err} (File '{tb.filename}', line {tb.lineno})", lv.ERROR)
		return []
