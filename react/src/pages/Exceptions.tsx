import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { RotateCcw, ShieldAlert } from 'lucide-react';
import { ButtonComponent } from '@syncfusion/ej2-react-buttons';
import { DropDownList, type ChangeEventArgs } from '@syncfusion/ej2-dropdowns';
import { GridComponent, ColumnsDirective, ColumnDirective, Inject as GridInject, Sort, Filter, Page } from '@syncfusion/ej2-react-grids';
import { KanbanComponent, ColumnsDirective as KanbanColumnsDirective, ColumnDirective as KanbanColumnDirective, type DragEventArgs } from '@syncfusion/ej2-react-kanban';
import { api, ApiError } from '../api/client.ts';
import type { ExceptionDto, ExceptionStatus } from '../api/types.ts';
import type { AuthState } from '../state/auth.ts';
import { exceptionTone, severityTone, isValidExceptionTransition } from '../domain/status.ts';
import { setSessionOverride, useSessionOverrides } from '../state/sessionOverrides.ts';

// The Move to cell hosts DropDownList with appendTo on an element the
// template creates. A React DropDownListComponent in this cell moves that
// input out of the node React owns, and the next row refresh throws
// removeChild. Immutable row reuse is left off: after a sort the grid
// keeps the old row nodes and the column templates stay blank.
// Public drag/grid moves stay client-side; a signed-in write calls PATCH
// and surfaces a real 400 rejection.

const STATUSES: ExceptionStatus[] = ['Open', 'Investigating', 'Resolved', 'Escalated'];

const openedFormat = new Intl.DateTimeFormat('en-US', {
  month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', timeZone: 'America/New_York',
});

function nextValidTargets(status: ExceptionStatus): ExceptionStatus[] {
  return STATUSES.filter(target => isValidExceptionTransition(status, target));
}

interface KanbanCard extends ExceptionDto {
  Id: string;
  Status: ExceptionStatus;
  Title: string;
  MoveTargets: ExceptionStatus[];
  SeverityTone: string;
  StatusTone: string;
  OpenedLabel: string;
}

let moveExceptionRef: (id: string, from: ExceptionStatus, to: ExceptionStatus) => void = () => {};

function exceptionCardTemplate(props: KanbanCard): ReactElement {
  return (
    <div className="board-card" data-tone={props.SeverityTone}>
      <div className="board-card-meta">
        <span className="status-badge" data-tone={props.SeverityTone}>{props.severity}</span>
        <span className="status-badge" data-tone={props.StatusTone}>{props.Status}</span>
      </div>
      <h3>{props.customerLabel}</h3>
      <p className="board-id">{props.kind}</p>
      <p className="board-id">Opened {props.OpenedLabel}</p>
      <p>{props.note}</p>
    </div>
  );
}

function severityTemplate(data: KanbanCard) {
  return <span className="status-badge" data-tone={data.SeverityTone}>{data.severity}</span>;
}

function statusTemplate(data: KanbanCard) {
  return <span className="status-badge" data-tone={data.StatusTone}>{data.Status}</span>;
}

function moveToTemplate(data: KanbanCard): ReactElement {
  const hostRef = useRef<HTMLDivElement>(null);
  const targets = data.MoveTargets.join('|');
  useEffect(() => {
    const host = hostRef.current;
    if (!host || data.MoveTargets.length === 0) return;
    const input = document.createElement('input');
    input.id = `exception-move-${data.Id}`;
    host.appendChild(input);
    const dropdown = new DropDownList({
      dataSource: data.MoveTargets,
      placeholder: 'Move to…',
      popupHeight: '180px',
      width: '100%',
      showClearButton: false,
      htmlAttributes: { 'aria-label': `Move ${data.customerLabel}'s exception to a new status` },
      change: (args: ChangeEventArgs) => {
        const value = args.value as ExceptionStatus | null | undefined;
        if (!args.isInteracted || !value) return;
        moveExceptionRef(data.Id, data.Status, value);
      },
    });
    dropdown.appendTo(input);
    return () => {
      if (!dropdown.isDestroyed) dropdown.destroy();
      host.replaceChildren();
    };
  }, [data.Id, data.Status, data.customerLabel, targets]);
  if (data.MoveTargets.length === 0) return <span className="muted">No further moves</span>;
  return <div ref={hostRef} className="move-to-dropdown" />;
}

export function Exceptions({ auth }: { auth: AuthState }) {
  const [exceptions, setExceptions] = useState<ExceptionDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);
  const overrides = useSessionOverrides();

  const load = () => {
    setError(null);
    api.getExceptions().then(setExceptions)
      .catch(err => setError(err instanceof ApiError ? err.message : 'Unable to reach the fleet API.'));
  };
  useEffect(load, []);

  const effective: ExceptionDto[] = useMemo(() => {
    if (!exceptions) return [];
    if (auth.token) return exceptions;
    return exceptions.map(exc => overrides.has(exc.id) ? { ...exc, status: overrides.get(exc.id) as ExceptionStatus } : exc);
  }, [exceptions, overrides, auth.token]);

  const cards: KanbanCard[] = useMemo(() => effective.map(exc => {
    const opened = new Date(exc.openedAt);
    return {
      ...exc,
      Id: exc.id,
      Status: exc.status,
      Title: `${exc.customerLabel} — ${exc.kind}`,
      MoveTargets: nextValidTargets(exc.status),
      SeverityTone: severityTone(exc.severity),
      StatusTone: exceptionTone(exc.status),
      OpenedLabel: Number.isNaN(opened.getTime()) ? exc.openedAt : openedFormat.format(opened),
    };
  }), [effective]);

  async function moveException(id: string, from: ExceptionStatus, to: ExceptionStatus): Promise<boolean> {
    setActionError(null);
    setActionNotice(null);
    if (!isValidExceptionTransition(from, to)) {
      setActionError(`Cannot move an exception from ${from} directly to ${to} — this skips the required triage step.`);
      return false;
    }
    if (auth.token) {
      try {
        const updated = await api.patchExceptionStatus(id, to, auth.token);
        setExceptions(prev => prev ? prev.map(e => e.id === id ? updated : e) : prev);
        setActionNotice(`Saved: exception moved to ${to} via the real API and persisted to PostgreSQL.`);
        return true;
      } catch (err) {
        setActionError(err instanceof ApiError ? `Server rejected the move: ${err.message}` : 'Unable to reach the fleet API.');
        return false;
      }
    }
    setSessionOverride(id, to);
    setActionNotice(`Simulated: exception moved to ${to} in this tab only.`);
    return true;
  }

  moveExceptionRef = (id, from, to) => { void moveException(id, from, to); };

  function onDragStop(args: DragEventArgs) {
    const data = args.data as unknown as KanbanCard | KanbanCard[];
    const card = Array.isArray(data) ? data[0] : data;
    if (!card) return;
    const from = effective.find(e => e.id === card.Id)?.status;
    const to = card.Status;
    if (!from || from === to) return;
    if (!isValidExceptionTransition(from, to)) {
      args.cancel = true;
      setActionError(`Cannot move an exception from ${from} directly to ${to} — this skips the required triage step.`);
      return;
    }
    void moveException(card.Id, from, to);
  }

  if (error) return <div className="notice error" role="alert">
    {error}
    <div className="form-actions" style={{ marginTop: 12 }}><ButtonComponent cssClass="action-button" onClick={load}><RotateCcw size={14} aria-hidden="true" /> Retry</ButtonComponent></div>
  </div>;

  if (!exceptions) return <div className="skeleton" style={{ height: 560 }} />;

  if (exceptions.length === 0) return <div className="empty-state"><ShieldAlert size={40} aria-hidden="true" /><h2>No exceptions in this scenario</h2></div>;

  return <div className="exceptions-layout">
    {actionError && <div className="notice error" role="alert">{actionError}</div>}
    {actionNotice && <div className="notice success" role="status">{actionNotice}</div>}
    <div className="notice info">
      {auth.token
        ? `Signed in as ${auth.email}. Moves call the real API and persist. Try moving an "Open" card straight to "Resolved" to see the server's real 400 rejection.`
        : 'Public session. Moves are simulated in this tab only.'}
    </div>

    <section className="panel" aria-label="Exception triage board">
      <div className="panel-heading"><div><h2>Triage board</h2><p>Drag a card, or use the Move to list in the grid below. Both use the same exceptions.</p></div></div>
      <div className="board-scroll">
        <KanbanComponent id="exceptions-kanban" keyField="Status" dataSource={cards}
          cardSettings={{ headerField: 'Id', template: exceptionCardTemplate }} dragStop={onDragStop}>
          <KanbanColumnsDirective>
            <KanbanColumnDirective headerText="Open" keyField="Open" allowToggle transitionColumns={['Investigating', 'Escalated']} />
            <KanbanColumnDirective headerText="Investigating" keyField="Investigating" allowToggle transitionColumns={['Resolved', 'Escalated']} />
            <KanbanColumnDirective headerText="Resolved" keyField="Resolved" allowToggle allowDrag={false} />
            <KanbanColumnDirective headerText="Escalated" keyField="Escalated" allowToggle transitionColumns={['Investigating', 'Resolved']} />
          </KanbanColumnsDirective>
        </KanbanComponent>
      </div>
    </section>

    <section className="panel" aria-label="Exceptions grid">
      <div className="panel-heading"><div><h2>All exceptions</h2><p>{cards.length} exceptions · sort, filter, search</p></div></div>
      <div className="grid-scroll">
        <GridComponent dataSource={cards} allowSorting allowFiltering allowPaging
          pageSettings={{ pageSize: 10 }} filterSettings={{ type: 'Menu' }} aria-label="All exceptions">
          <ColumnsDirective>
            <ColumnDirective field="Id" isPrimaryKey visible={false} />
            <ColumnDirective field="customerLabel" headerText="Customer" width="160" />
            <ColumnDirective field="kind" headerText="Kind" width="120" />
            <ColumnDirective field="severity" headerText="Severity" width="110" template={severityTemplate} />
            <ColumnDirective field="Status" headerText="Status" width="130" template={statusTemplate} />
            <ColumnDirective field="openedAt" headerText="Opened" width="170" type="datetime" format="MM/dd/yyyy hh:mm a" />
            <ColumnDirective field="note" headerText="Note" width="220" />
            <ColumnDirective headerText="Move to" width="220" template={moveToTemplate} allowSorting={false} allowFiltering={false} />
          </ColumnsDirective>
          <GridInject services={[Sort, Filter, Page]} />
        </GridComponent>
      </div>
    </section>
  </div>;
}
