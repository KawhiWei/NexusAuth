import { RouterProvider } from 'react-router-dom';
import { router } from '.';

export default function RootRouterProvider() {
    return (
        <RouterProvider router={router} />
    )
}


