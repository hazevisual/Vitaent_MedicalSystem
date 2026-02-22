import { useEffect } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { loadBranding } from './branding/loadBranding';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { SchedulePage } from './pages/SchedulePage';
import { useAuth } from './auth/AuthContext';

function PrivateRoute({ children }: { children: JSX.Element }) {
  const { accessToken } = useAuth();
  return accessToken ? children : <Navigate to="/login" replace />;
}

export function App() {
  useEffect(() => {
    void loadBranding();
  }, []);

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <PrivateRoute>
            <DashboardPage />
          </PrivateRoute>
        }
      />
          <Route
        path="/schedule"
        element={
          <PrivateRoute>
            <SchedulePage />
          </PrivateRoute>
        }
      />
    </Routes>
  );
}
