import ReactDOM from "react-dom/client";
import App from "./App";
import { AuthProvider } from "./context/AuthContext";
import { TenantContextProvider } from "./context/TenantContextProvider";
import { AcademicUiProvider } from "./context/AcademicUiContext";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <AuthProvider>
    <TenantContextProvider>
      <AcademicUiProvider>
        <App />
      </AcademicUiProvider>
    </TenantContextProvider>
  </AuthProvider>
);
