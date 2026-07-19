import ReactDOM from "react-dom/client";
import App from "./App";
import { AuthProvider } from "./context/AuthContext";
import { TenantContextProvider } from "./context/TenantContextProvider";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <AuthProvider>
    <TenantContextProvider>
      <App />
    </TenantContextProvider>
  </AuthProvider>
);
