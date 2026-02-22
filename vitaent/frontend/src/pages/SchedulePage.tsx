import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '../api/client';
import { useAuth } from '../auth/AuthContext';

type Doctor = {
  id: string;
  name: string;
  isActive: boolean;
  createdAt: string;
};

type Appointment = {
  id: string;
  doctorId: string;
  patientName: string;
  startsAt: string;
  endsAt: string;
  status: string;
  createdAt: string;
};

type ValidationProblem = {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
};

function toIsoLocalDateTime(value: Date) {
  const tzOffsetMs = value.getTimezoneOffset() * 60000;
  return new Date(value.getTime() - tzOffsetMs).toISOString().slice(0, 16);
}

function parseApiError(error: unknown): ValidationProblem {
  if (!(error instanceof Error)) {
    return { title: 'Request failed', detail: 'Unexpected error' };
  }

  try {
    return JSON.parse(error.message) as ValidationProblem;
  } catch {
    return { title: 'Request failed', detail: error.message };
  }
}

export function SchedulePage() {
  const { accessToken } = useAuth();
  const queryClient = useQueryClient();

  const now = new Date();
  const defaultFrom = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);
  const defaultTo = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);

  const [doctorId, setDoctorId] = useState('');
  const [patientName, setPatientName] = useState('');
  const [startsAtLocal, setStartsAtLocal] = useState(toIsoLocalDateTime(new Date(now.getTime() + 30 * 60 * 1000)));
  const [durationMinutes, setDurationMinutes] = useState(30);
  const [fromLocal, setFromLocal] = useState(toIsoLocalDateTime(defaultFrom));
  const [toLocal, setToLocal] = useState(toIsoLocalDateTime(defaultTo));
  const [formErrors, setFormErrors] = useState<Record<string, string[]>>({});
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [statusErrorMessage, setStatusErrorMessage] = useState<string | null>(null);

  const doctorsQuery = useQuery({
    queryKey: ['doctors'],
    queryFn: () => apiFetch<Doctor[]>('/api/doctors?tenant=clinic1', { token: accessToken })
  });

  const fromIso = useMemo(() => new Date(fromLocal).toISOString(), [fromLocal]);
  const toIso = useMemo(() => new Date(toLocal).toISOString(), [toLocal]);

  const appointmentsQuery = useQuery({
    queryKey: ['appointments', fromIso, toIso],
    queryFn: () =>
      apiFetch<Appointment[]>(`/api/appointments?tenant=clinic1&from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}`, {
        token: accessToken
      })
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      const startsAt = new Date(startsAtLocal);
      const endsAt = new Date(startsAt.getTime() + durationMinutes * 60 * 1000);

      return apiFetch<Appointment>('/api/appointments?tenant=clinic1', {
        method: 'POST',
        token: accessToken,
        body: JSON.stringify({
          doctorId,
          patientName,
          startsAt: startsAt.toISOString(),
          endsAt: endsAt.toISOString()
        })
      });
    },
    onSuccess: async () => {
      setPatientName('');
      setFormErrors({});
      setErrorMessage(null);
      await queryClient.invalidateQueries({ queryKey: ['appointments'] });
    },
    onError: (error) => {
      const payload = parseApiError(error);
      setFormErrors(payload.errors ?? {});
      setErrorMessage(payload.detail ?? payload.title ?? 'Unable to create appointment');
    }
  });

  const updateStatusMutation = useMutation({
    mutationFn: async ({ appointmentId, status }: { appointmentId: string; status: 'Confirmed' | 'Cancelled' }) =>
      apiFetch<Appointment>(`/api/appointments/${appointmentId}/status?tenant=clinic1`, {
        method: 'PATCH',
        token: accessToken,
        body: JSON.stringify({ status })
      }),
    onSuccess: async () => {
      setStatusErrorMessage(null);
      await queryClient.invalidateQueries({ queryKey: ['appointments'] });
    },
    onError: (error) => {
      const payload = parseApiError(error);
      const fieldMessage = payload.errors?.status?.join(', ');
      setStatusErrorMessage(fieldMessage ?? payload.detail ?? payload.title ?? 'Unable to update appointment status');
    }
  });

  useEffect(() => {
    if (!doctorId && doctorsQuery.data?.length) {
      setDoctorId(doctorsQuery.data[0].id);
    }
  }, [doctorId, doctorsQuery.data]);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    setFormErrors({});
    setErrorMessage(null);
    setStatusErrorMessage(null);
    createMutation.mutate();
  };

  return (
    <div className="min-h-screen bg-slate-50 p-8">
      <div className="max-w-4xl mx-auto bg-white rounded-xl shadow p-6 space-y-6">
        <h1 className="text-2xl font-semibold text-brandPrimary">Schedule</h1>

        <form className="grid gap-4 md:grid-cols-2" onSubmit={onSubmit}>
          <div>
            <label className="block text-sm mb-1">Doctor</label>
            <select
              className="w-full border rounded-lg px-3 py-2"
              value={doctorId}
              onChange={(e) => setDoctorId(e.target.value)}
            >
              <option value="">Select doctor</option>
              {(doctorsQuery.data ?? []).map((doctor) => (
                <option key={doctor.id} value={doctor.id}>
                  {doctor.name}
                </option>
              ))}
            </select>
            {formErrors.doctorId && <p className="text-sm text-red-600">{formErrors.doctorId.join(', ')}</p>}
          </div>

          <div>
            <label className="block text-sm mb-1">Patient name</label>
            <input
              className="w-full border rounded-lg px-3 py-2"
              value={patientName}
              onChange={(e) => setPatientName(e.target.value)}
            />
            {formErrors.patientName && <p className="text-sm text-red-600">{formErrors.patientName.join(', ')}</p>}
          </div>

          <div>
            <label className="block text-sm mb-1">Starts at</label>
            <input
              className="w-full border rounded-lg px-3 py-2"
              type="datetime-local"
              value={startsAtLocal}
              onChange={(e) => setStartsAtLocal(e.target.value)}
            />
            {formErrors.startsAt && <p className="text-sm text-red-600">{formErrors.startsAt.join(', ')}</p>}
          </div>

          <div>
            <label className="block text-sm mb-1">Duration (minutes)</label>
            <select
              className="w-full border rounded-lg px-3 py-2"
              value={durationMinutes}
              onChange={(e) => setDurationMinutes(Number(e.target.value))}
            >
              {[15, 30, 45, 60].map((minutes) => (
                <option key={minutes} value={minutes}>
                  {minutes}
                </option>
              ))}
            </select>
            {formErrors.duration && <p className="text-sm text-red-600">{formErrors.duration.join(', ')}</p>}
          </div>

          <div className="md:col-span-2">
            <button className="rounded-lg bg-brandPrimary text-white px-4 py-2" disabled={createMutation.isPending}>
              {createMutation.isPending ? 'Creating...' : 'Create'}
            </button>
          </div>
        </form>

        {errorMessage && <p className="text-sm text-red-600">{errorMessage}</p>}

        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="block text-sm mb-1">From</label>
            <input
              className="w-full border rounded-lg px-3 py-2"
              type="datetime-local"
              value={fromLocal}
              onChange={(e) => setFromLocal(e.target.value)}
            />
          </div>
          <div>
            <label className="block text-sm mb-1">To</label>
            <input
              className="w-full border rounded-lg px-3 py-2"
              type="datetime-local"
              value={toLocal}
              onChange={(e) => setToLocal(e.target.value)}
            />
          </div>
        </div>

        <div>
          <h2 className="text-lg font-medium mb-2">Appointments</h2>
          {statusErrorMessage && <p className="mb-2 text-sm text-red-600">{statusErrorMessage}</p>}
          {appointmentsQuery.isLoading ? (
            <p>Loading...</p>
          ) : (
            <ul className="space-y-2">
              {(appointmentsQuery.data ?? []).map((appointment) => (
                <li key={appointment.id} className="border rounded-lg p-3 text-sm">
                  <p className="font-medium">{appointment.patientName}</p>
                  <p>
                    {new Date(appointment.startsAt).toLocaleString()} - {new Date(appointment.endsAt).toLocaleString()}
                  </p>
                  <p>Status: {appointment.status}</p>
                  {appointment.status !== 'Cancelled' && (
                    <div className="mt-2 flex gap-2">
                      {appointment.status === 'Pending' && (
                        <button
                          className="rounded border px-3 py-1"
                          disabled={updateStatusMutation.isPending}
                          onClick={() => updateStatusMutation.mutate({ appointmentId: appointment.id, status: 'Confirmed' })}
                          type="button"
                        >
                          Confirm
                        </button>
                      )}
                      {(appointment.status === 'Pending' || appointment.status === 'Confirmed') && (
                        <button
                          className="rounded border px-3 py-1"
                          disabled={updateStatusMutation.isPending}
                          onClick={() => updateStatusMutation.mutate({ appointmentId: appointment.id, status: 'Cancelled' })}
                          type="button"
                        >
                          Cancel
                        </button>
                      )}
                    </div>
                  )}
                </li>
              ))}
              {!appointmentsQuery.data?.length && <li className="text-sm text-slate-500">No appointments in range.</li>}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
